using Gradient.CryptoAnalysis.Research;
using Flag = Gradient.CryptoAnalysis.Research.KnownStructure.EnumFlag;

namespace Gradient.CryptoAnalysis.Test.Research;

[TestClass]
public class KnownStructureTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<Price> Closes(params double[] closes) =>
        closes.Select((c, i) => new Price { DateTime = Start.AddHours(i), Open = c, High = c, Low = c, Close = c }).ToList();

    private static string At(List<Price> prices, int level, Flag flag) =>
        string.Join(" ", KnownStructure.Met(prices, level, flag).Select((met, i) => met ? i : -1).Where(i => i >= 0));

    [TestMethod]
    public void Met_AreTheReplaysEventsOnTheCandlesTheyBecomeKnown()
    {
        var prices = Closes(10, 12, 11, 13, 12, 15);

        Assert.AreEqual("3 5", At(prices, 1, Flag.BreakUp));
        Assert.AreEqual("5", At(prices, 1, Flag.Uptrend));
        Assert.AreEqual("", At(prices, 1, Flag.BreakDown));
    }

    [TestMethod]
    public void Met_PresumedDeathLegDowntrendHoldsUntilTheNewHigh()
    {
        var prices = Closes(10, 20, 17, 18, 15, 16, 14, 21);

        Assert.AreEqual("4 6", At(prices, 1, Flag.BreakDown));
        Assert.AreEqual("6", At(prices, 1, Flag.Downtrend));
        Assert.AreEqual("7", At(prices, 1, Flag.BreakUp));
    }

    [TestMethod]
    public void Breaks_SayWhatEachWasReadFrom()
    {
        // Upswings from the highs of 12 and 13; the close of 10.5 is below the first's Swing Low of 11, inside the second.
        var breaks = KnownStructure.Breaks(Closes(10, 12, 11, 13, 10.5, 12.5, 14));

        Assert.AreEqual("3:BreakOfStructure Up from 1, 1 in a row from 1; 4:MarketStructureBreak Down from 2; 6:BreakOfStructure Up from 3, 2 in a row from 1, Weak",
            string.Join("; ", breaks.SelectMany((x, i) => x.Where(b => b.Level == 1).Select(b => b.Kind == EnumKnownBreak.MarketStructureBreak
                ? $"{i}:{b.Kind} {b.Direction} from {b.From}"
                : $"{i}:{b.Kind} {b.Direction} from {b.From}, {b.Run} in a row from {b.Begins}{(b.Weak ? ", Weak" : "")}"))));
    }

    // What the last of these candles shows, read from every one of them by the Sawtooth: its breaks, and the levels a
    // trend holds at each way.
    private static (List<string> Breaks, List<string> Trends) Plain(List<Price> prices)
    {
        var last = prices[^1].DateTime;
        int At(DateTime time) => prices.FindIndex(x => x.DateTime == time);
        var sawtooth = Sawtooth.Levels(prices, EnumPriceBasis.Close, 64);
        var (made, held) = (new List<string>(), new List<string>());
        for (var level = 1; level < sawtooth.Count; level++)
        {
            var swings = Sawtooth.Swings(prices, sawtooth, EnumPriceBasis.Close, level);
            var breaks = Sawtooth.MarketStructureBreaks(prices, swings, EnumPriceBasis.Close);
            held.AddRange(Sawtooth.Trends(swings, breaks).Where(x => x.End == null).Select(x => $"{level} {x.Direction}").Distinct());

            var ordered = swings.Where(x => x.BreakOfStructure != null).OrderBy(x => x.BreakOfStructure!.Time).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var swing = ordered[i];
                if (swing.BreakOfStructure!.Time != last)
                    continue;
                var run = 1;
                while (i - run >= 0 && ordered[i - run].Direction == swing.Direction)
                    run++;
                var against = swing.Direction == EnumSwingDirection.Up ? EnumAnnotationType.BearishMarketStructureBreak : EnumAnnotationType.BullishMarketStructureBreak;
                var weak = breaks.Any(b => b.Type == against && b.Break.Time > swing.Start.Time && b.Break.Time < last);
                made.Add(new KnownBreak(level, EnumKnownBreak.BreakOfStructure, swing.Direction, At(swing.Start.Time), At(ordered[i - run + 1].Start.Time), run, weak).ToString());
            }

            foreach (var b in breaks.Where(x => x.Break.Time == last))
            {
                var from = At(b.Reference.Time);
                made.Add(new KnownBreak(level, EnumKnownBreak.MarketStructureBreak, b.Type == EnumAnnotationType.BullishMarketStructureBreak ? EnumSwingDirection.Up : EnumSwingDirection.Down, from, from).ToString());
            }
        }

        return (made.Order().ToList(), held.Order().ToList());
    }

    private static void AssertIsThePlainReading(List<Price> prices, IEnumerable<int> candles)
    {
        var breaks = KnownStructure.Breaks(prices);
        var trends = Enumerable.Range(1, KnownStructure.Levels).SelectMany(level => new[] { Flag.Uptrend, Flag.Downtrend }
            .Select(flag => (level, flag, met: KnownStructure.Met(prices, level, flag)))).ToList();
        foreach (var t in candles)
        {
            var plain = Plain(prices.GetRange(0, t + 1));
            Assert.AreEqual(string.Join("; ", plain.Breaks), string.Join("; ", breaks[t].Select(x => x.ToString()).Order()), $"#{t}");
            Assert.AreEqual(string.Join("; ", plain.Trends),
                string.Join("; ", trends.Where(x => x.met[t]).Select(x => $"{x.level} {(x.flag == Flag.Uptrend ? EnumSwingDirection.Up : EnumSwingDirection.Down)}").Order()), $"#{t} trends");
        }
    }

    [TestMethod]
    public void Breaks_AreThoseOfEveryCandleToEachReadAgain_OnARandomWalk()
    {
        var prices = Synthetic.Walk(11, Start, 700);

        AssertIsThePlainReading(prices, Enumerable.Range(0, prices.Count));
    }

    [TestMethod]
    public void Breaks_AreThoseOfEveryCandleToEachReadAgain_OnBtc()
    {
        var prices = Btc.From2023().Take(2500).ToList();

        AssertIsThePlainReading(prices, Enumerable.Range(0, prices.Count).Where(t => t % 37 == 0 || t > prices.Count - 100));
    }

    [TestMethod]
    public void Breaks_AreTheFlagsBreaksAndCountWhatTheTrendFlagsHold()
    {
        var prices = Synthetic.Walk(5, Start, 1500);

        var breaks = KnownStructure.Breaks(prices);

        var levels = breaks.SelectMany(x => x).Max(x => x.Level);
        for (var level = 1; level <= levels; level++)
        {
            var flags = Enum.GetValues<Flag>().ToDictionary(x => x, x => KnownStructure.Met(prices, level, x));
            for (var i = 0; i < prices.Count; i++)
            {
                bool Made(EnumKnownBreak kind, EnumSwingDirection direction) => breaks[i].Any(x => x.Level == level && x.Kind == kind && x.Direction == direction);
                Assert.AreEqual(flags[Flag.BreakUp][i], Made(EnumKnownBreak.BreakOfStructure, EnumSwingDirection.Up), $"#{i} level {level}");
                Assert.AreEqual(flags[Flag.BreakDown][i], Made(EnumKnownBreak.BreakOfStructure, EnumSwingDirection.Down), $"#{i} level {level}");
                Assert.AreEqual(flags[Flag.BullishMarketStructureBreak][i], Made(EnumKnownBreak.MarketStructureBreak, EnumSwingDirection.Up), $"#{i} level {level}");
                Assert.AreEqual(flags[Flag.BearishMarketStructureBreak][i], Made(EnumKnownBreak.MarketStructureBreak, EnumSwingDirection.Down), $"#{i} level {level}");

                // A break of structure that makes two or more in a row is a Trend holding, and one that makes one is none.
                foreach (var b in breaks[i].Where(x => x.Level == level && x.Kind == EnumKnownBreak.BreakOfStructure))
                {
                    Assert.AreEqual(b.Run >= 2, flags[b.Direction == EnumSwingDirection.Up ? Flag.Uptrend : Flag.Downtrend][i], $"#{i} level {level}");
                    Assert.IsTrue(b.Begins <= b.From && b.From < i && (b.Run > 1 || b.Begins == b.From), $"#{i} level {level}");
                }
            }
        }

        Assert.IsTrue(breaks.Sum(x => x.Count(b => b.Run >= 3)) > 20, "A random walk has Trends of three Swings and more.");
    }

    [TestMethod]
    public void Breaks_OfACandleDoNotChangeWithTheCandlesAfterIt()
    {
        var prices = Synthetic.Walk(3, Start, 900);

        var all = KnownStructure.Breaks(prices);
        var before = KnownStructure.Breaks(prices.Take(600).ToList());

        for (var i = 0; i < before.Length; i++)
            CollectionAssert.AreEqual(before[i].ToList(), all[i].ToList(), $"#{i}");
        Assert.IsTrue(all.Count(x => x.Count > 0) > 200, "A random walk breaks structure on many candles.");
    }

    [TestMethod]
    public void Store_IsTheOneHeldForTheSeries_WhenThereIsOne()
    {
        var prices = Synthetic.Walk(9, Start, 300);
        var held = StructureStore.Read(prices);
        try
        {
            KnownStructure.Stores = (p, _, _) => ReferenceEquals(p, prices) ? held : null;

            Assert.AreSame(held, KnownStructure.Store(prices, Ranges.DefaultMinRetracement, Ranges.DefaultBand));
            Assert.AreNotSame(held, KnownStructure.Store(prices.ToList(), Ranges.DefaultMinRetracement, Ranges.DefaultBand));
        }
        finally
        {
            KnownStructure.Stores = null;
        }
    }
}
