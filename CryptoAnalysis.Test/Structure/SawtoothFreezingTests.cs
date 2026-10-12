namespace Gradient.CryptoAnalysis.Test.Structure;

/// <summary>
/// What a new candle can and cannot change in the structure read from every candle before it, shown on real BTC candles a
/// candle at a time: what a store of the structure may keep as it is, and what it must look at again.
/// </summary>
[TestClass]
public class SawtoothFreezingTests
{
    // More levels than any stretch here has, so every level is read.
    private const int Depth = 60;

    private sealed record Item(int Level, DateTime Last);

    /// <summary>
    /// The structure of the first candles of a stretch: the highest high, the pivots that can no longer move, and the
    /// Swings, market structure breaks and Trends, each by what it is made of and not by its level.
    /// </summary>
    private sealed record Reading(DateTime Last, SawtoothPivot High, Dictionary<int, (int Level, EnumPivotKind Kind)> Pivots, Dictionary<int, (int Level, EnumPivotKind Kind)> Fixed,
        Dictionary<string, Item> Swings, Dictionary<string, Item> Breaks, Dictionary<string, Item> Trends);

    private static Reading Read(List<Price> all, int count)
    {
        var prices = all.GetRange(0, count);
        var levels = Sawtooth.Levels(prices, EnumPriceBasis.Close, Depth);
        var pivots = new Dictionary<int, (int, EnumPivotKind)>();
        var fixedPivots = new Dictionary<int, (int, EnumPivotKind)>();
        // A pivot is carried down to every finer level: its level is the first it is at.
        foreach (var level in levels)
        {
            foreach (var p in level.Pivots.Where(x => x.Kind is EnumPivotKind.High or EnumPivotKind.Low))
            {
                if (pivots.TryAdd(p.Index, (level.Level, p.Kind)) && p.ConfirmedIndex != null)
                    fixedPivots.Add(p.Index, (level.Level, p.Kind));
            }
        }

        var swings = new Dictionary<string, Item>();
        var breaks = new Dictionary<string, Item>();
        var trends = new Dictionary<string, Item>();
        for (var level = 1; level < levels.Count; level++)
        {
            var s = Sawtooth.Swings(prices, levels, EnumPriceBasis.Close, level);
            var b = Sawtooth.MarketStructureBreaks(prices, s, EnumPriceBasis.Close);
            foreach (var x in s)
                swings.Add($"{x.Direction} {x.Start.Time:yyMMddHH} extreme {x.Extreme.Time:yyMMddHH} break {x.BreakOfStructure!.Time:yyMMddHH}", new Item(level, x.BreakOfStructure.Time));
            foreach (var x in b)
                breaks.Add($"{x.Type} of {x.Reference.Time:yyMMddHH} at {x.Break.Time:yyMMddHH}", new Item(level, x.Break.Time));
            foreach (var x in Sawtooth.Trends(s, b))
                trends.Add($"{x.Direction} {x.Start.Time:yyMMddHH} confirmed {x.Confirmed.Time:yyMMddHH}", new Item(level, x.Confirmed.Time));
        }

        return new Reading(prices[^1].DateTime, levels[0].Pivots.First(x => x.Kind == EnumPivotKind.High), pivots, fixedPivots, swings, breaks, trends);
    }

    private static Reading[] Readings(List<Price> all, int from, int to)
    {
        var readings = new Reading[to - from + 1];
        Parallel.For(0, readings.Length, i => readings[i] = Read(all, from + i));
        return readings;
    }

    // The first months of 2023, a candle at a time: new highs through January and April, and falls between and after.
    private static readonly Lazy<Reading[]> Rally = new(() => Readings(Btc.From2023(), 200, 3000));

    // From 2020, the candles either side of the close above the high of November 2021, in March 2024.
    private static readonly Lazy<Reading[]> Crossing = new(() =>
    {
        var all = Btc.From2020();
        var high = Enumerable.Range(0, 30000).MaxBy(i => all[i].Close);
        var crossing = Enumerable.Range(30000, all.Count - 30000).First(i => all[i].Close > all[high].Close);
        return Readings(all, crossing - 40, crossing + 40);
    });

    /// <summary>
    /// Each candle added to each stretch: the reading before it, the reading with it, and whether it made a new highest high.
    /// </summary>
    private static IEnumerable<(Reading Before, Reading After, bool NewHigh)> Steps() =>
        new[] { Rally.Value, Crossing.Value }.SelectMany(x => x.Zip(x.Skip(1), (a, b) => (a, b, b.High.Index != a.High.Index)));

    [TestMethod]
    public void Stretches_HaveNewHighsAndCandlesBetween()
    {
        Assert.IsTrue(Steps().Count(x => x.NewHigh) > 50);
        Assert.IsTrue(Steps().Count(x => !x.NewHigh) > 2000);
        Assert.IsTrue(Crossing.Value.Zip(Crossing.Value.Skip(1)).Any(x => x.Second.High.Index - x.First.High.Index > 20000), "The high of 2021 is passed.");
    }

    [TestMethod]
    public void APivot_OnceItCannotMove_StaysAPivotOfItsKind()
    {
        foreach (var (before, after, _) in Steps())
        {
            foreach (var (index, was) in before.Fixed)
            {
                Assert.IsTrue(after.Pivots.TryGetValue(index, out var now), $"The pivot at candle {index} is gone at {after.Last:u}.");
                Assert.AreEqual(was.Kind, now.Kind, $"The pivot at candle {index} at {after.Last:u}.");
            }
        }
    }

    [TestMethod]
    public void APivotsLevel_FallsWhenALegIsSettled_AndRisesByOneOnlyOnANewHigh()
    {
        var rose = 0;
        var fell = 0;
        foreach (var (before, after, newHigh) in Steps())
        {
            foreach (var (index, was) in before.Fixed)
            {
                var now = after.Pivots[index].Level;
                if (now > was.Level)
                {
                    Assert.IsTrue(newHigh, $"The pivot at candle {index} went from level {was.Level} to {now} at {after.Last:u} with no new high.");
                    Assert.AreEqual(was.Level + 1, now, $"The pivot at candle {index} at {after.Last:u}.");
                    // The old high is itself one of them: it goes from level 0 to level 1.
                    Assert.IsTrue(index >= before.High.Index, $"The pivot at candle {index} is before the high it was read under.");
                    rose++;
                }
                else if (now < was.Level)
                {
                    fell++;
                }
            }
        }

        Assert.IsTrue(rose > 0 && fell > 0, "Levels move both ways in these stretches.");
    }

    [TestMethod]
    public void BeforeTheHighestHigh_NothingChanges()
    {
        foreach (var (before, after, _) in Steps())
        {
            foreach (var (index, was) in before.Pivots.Where(x => x.Key < before.High.Index))
                Assert.AreEqual(was, after.Pivots.GetValueOrDefault(index), $"The pivot at candle {index} at {after.Last:u}.");
            Assert.AreEqual(before.Pivots.Keys.Count(x => x < before.High.Index), after.Pivots.Keys.Count(x => x < before.High.Index), $"Pivots before the high at {after.Last:u}.");

            foreach (var (name, of) in Kinds)
            {
                string Settled(Reading reading) => string.Join("\n", of(reading).Where(x => x.Value.Last < before.High.Time).Select(x => $"L{x.Value.Level} {x.Key}").Order());
                Assert.AreEqual(Settled(before), Settled(after), $"{name} before the high at {after.Last:u}.");
            }
        }
    }

    [TestMethod]
    public void ASwing_OnceConfirmed_StaysAsItIs_ThoughItsLevelMoves()
    {
        var moved = 0;
        foreach (var (before, after, _) in Steps())
        {
            foreach (var (swing, was) in before.Swings)
            {
                Assert.IsTrue(after.Swings.TryGetValue(swing, out var now), $"The Swing {swing} is gone at {after.Last:u}.");
                if (now!.Level != was.Level)
                    moved++;
            }

            // A Swing that is new is Confirmed by the new candle: none is found afterwards among the candles before it.
            foreach (var (swing, item) in after.Swings.Where(x => !before.Swings.ContainsKey(x.Key)))
                Assert.AreEqual(after.Last, item.Last, $"The Swing {swing} was not there at {before.Last:u}.");
        }

        Assert.IsTrue(moved > 0, "Swings change level in these stretches.");
    }

    [TestMethod]
    public void MarketStructureBreaksAndTrends_AreOfALevel_SoCanBeUnmadeAndMadeLater()
    {
        foreach (var (name, of) in Kinds.Skip(1))
        {
            Assert.IsTrue(Steps().Any(x => of(x.Before).Keys.Any(k => !of(x.After).ContainsKey(k))), $"{name}: one that was there is gone a candle later.");
        }

        Assert.IsTrue(Steps().Any(x => x.After.Trends.Any(t => !x.Before.Trends.ContainsKey(t.Key) && t.Value.Last < x.After.Last)),
            "A Trend is found among candles it was not found in before.");
    }

    [TestMethod]
    public void AMarketStructureBreak_IsUnmade_WhenItsSwingsLevelMoves()
    {
        // 9 May 2022: the 10:00 close settles a leg, Swings move up a level, and a break made a month before is no longer one.
        var all = Btc.From2020();
        var at = all.FindIndex(x => x.DateTime == new DateTime(2022, 5, 9, 10, 0, 0, DateTimeKind.Utc));
        const string made = "BearishMarketStructureBreak of 22032516 at 22040613";

        Assert.AreEqual(3, Read(all, at).Breaks[made].Level);
        Assert.IsFalse(Read(all, at + 1).Breaks.ContainsKey(made));
    }

    private static readonly (string Name, Func<Reading, Dictionary<string, Item>> Of)[] Kinds =
    [
        ("Swings", x => x.Swings),
        ("Market structure breaks", x => x.Breaks),
        ("Trends", x => x.Trends),
    ];
}
