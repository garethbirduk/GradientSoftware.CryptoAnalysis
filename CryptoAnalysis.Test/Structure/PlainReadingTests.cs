using System.Text.Json;

namespace Gradient.CryptoAnalysis.Test.Structure;

/// <summary>
/// Trends, retracements and ranges are found by searching lists kept in order. Here each is held to the plain reading it
/// stands for, which looks at every item each time, on real BTC candles at every level.
/// </summary>
[TestClass]
public class PlainReadingTests
{
    private const EnumPriceBasis Basis = EnumPriceBasis.Close;

    private sealed record Level(int Number, List<SwingOutline> Swings, List<MarketStructureBreakOutline> Breaks, List<TrendOutline> Trends, List<RetracementOutline> Retracements);

    private static readonly Lazy<(List<Price> Prices, List<Level> Levels)> Read = new(() =>
    {
        var prices = Btc.From2020().GetRange(0, 20000);
        var levels = Sawtooth.Levels(prices, Basis, 9);
        return (prices, Enumerable.Range(1, levels.Count - 1).Select(l =>
        {
            var swings = Sawtooth.Swings(prices, levels, Basis, l).OrderBy(x => x.Start.Time).ToList();
            var breaks = Sawtooth.MarketStructureBreaks(prices, swings, Basis);
            return new Level(l, swings, breaks, Sawtooth.Trends(swings, breaks), Sawtooth.Retracements(prices, swings, Sawtooth.Candidates(prices, levels, Basis, l), Basis));
        }).ToList());
    });

    [TestMethod]
    public void ATrendsBreaksAndStrongSwings_AreThoseCountedOneByOne()
    {
        var trends = 0;
        foreach (var level in Read.Value.Levels)
        {
            var ordered = level.Swings.OrderBy(x => x.BreakOfStructure!.Time).ToList();
            foreach (var trend in level.Trends)
            {
                var against = trend.Direction == EnumSwingDirection.Up ? EnumAnnotationType.BearishMarketStructureBreak : EnumAnnotationType.BullishMarketStructureBreak;
                var first = ordered.First(x => x.Start == trend.Start && x.Direction == trend.Direction);
                Assert.AreEqual(level.Breaks.Count(b => b.Type == against && b.Break.Time > first.BreakOfStructure!.Time && (trend.End == null || b.Break.Time <= trend.End.Time)),
                    trend.MarketStructureBreaks, $"Level {level.Number}, the trend from {trend.Start.Time:u}.");

                var parts = ordered.GetRange(ordered.IndexOf(first), trend.Swings);
                Assert.AreEqual(string.Join("", parts.Select(s => level.Breaks.Any(b => b.Type == against && b.Break.Time > s.Start.Time && b.Break.Time < s.BreakOfStructure!.Time) ? "w" : "S")),
                    string.Join("", trend.Parts.Select(x => x.Strong ? "S" : "w")), $"Level {level.Number}, the trend from {trend.Start.Time:u}.");
                trends++;
            }
        }

        Assert.IsTrue(trends > 500, $"Only {trends} trends.");
        Assert.IsTrue(Read.Value.Levels.SelectMany(x => x.Trends).Any(x => x.Parts.Any(p => !p.Strong)) && Read.Value.Levels.SelectMany(x => x.Trends).Any(x => x.MarketStructureBreaks > 0));
    }

    [TestMethod]
    public void ARetracement_IsFromTheLastSwingToBreakStructureBeforeItStarts()
    {
        var read = 0;
        foreach (var level in Read.Value.Levels)
        {
            var confirmed = level.Swings.OrderBy(x => x.BreakOfStructure!.Time).ToList();
            foreach (var swing in confirmed)
            {
                var previous = confirmed.LastOrDefault(x => x.BreakOfStructure!.Time <= swing.Start.Time && x.Start.Time < swing.Start.Time);
                var has = level.Retracements.FirstOrDefault(x => x.Confirmed && x.Direction == swing.Direction && x.To == swing.Start);
                if (has != null)
                {
                    Assert.AreEqual(previous?.Extreme, has.From, $"Level {level.Number}, the swing from {swing.Start.Time:u}.");
                    Assert.AreEqual(swing.Direction, previous!.Direction);
                    read++;
                }
                else if (previous?.Direction == swing.Direction)
                {
                    // One the plain reading would measure has none only when the move is too small to read against.
                    var gap = Math.Abs(swing.Start.Price - previous.Extreme.Price);
                    Assert.IsTrue((swing.Direction == EnumSwingDirection.Up ? swing.Start.Price <= previous.Extreme.Price : swing.Start.Price >= previous.Extreme.Price)
                        || gap < Math.Abs(previous.Extreme.Price) * Sawtooth.MinimumRetracementMovePercent / 100, $"Level {level.Number}, the swing from {swing.Start.Time:u} has no retracement.");
                }
            }
        }

        Assert.IsTrue(read > 2000, $"Only {read} retracements.");
    }

    [TestMethod]
    public void TheRanges_AreThoseFoundLookingAtEveryTrendSwingAndBreakEachTime()
    {
        var (prices, levels) = Read.Value;
        var found = 0;
        for (var l = 0; l + 1 < levels.Count; l++)
        {
            var expected = PlainFind(prices, levels[l].Retracements, levels[l].Swings, levels[l].Trends, levels[l + 1].Breaks, Ranges.DefaultMinRetracement, Ranges.DefaultBand);
            var ranges = Ranges.Find(prices, Basis, levels[l].Retracements, levels[l].Swings, levels[l].Trends, levels[l + 1].Breaks);
            Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(ranges), $"Level {levels[l].Number}.");

            // And with a lower bar, so that more are found and more are passed over while another holds.
            Assert.AreEqual(JsonSerializer.Serialize(PlainFind(prices, levels[l].Retracements, levels[l].Swings, levels[l].Trends, levels[l + 1].Breaks, 40, 5)),
                JsonSerializer.Serialize(Ranges.Find(prices, Basis, levels[l].Retracements, levels[l].Swings, levels[l].Trends, levels[l + 1].Breaks, 40, 5)), $"Level {levels[l].Number}, a lower bar.");
            found += ranges.Count;
        }

        Assert.IsTrue(found > 20, $"Only {found} ranges.");
    }

    // Ranges.Find as it was first written.
    private static List<RangeOutline> PlainFind(List<Price> prices, IReadOnlyList<RetracementOutline> retracements, IReadOnlyList<SwingOutline> swings,
        IReadOnlyList<TrendOutline> trends, IReadOnlyList<MarketStructureBreakOutline> finerBreaks, double minRetracement, double band)
    {
        var index = new Dictionary<DateTime, int>();
        for (var i = 0; i < prices.Count; i++)
            index.TryAdd(prices[i].DateTime, i);

        var found = new List<RangeOutline>();
        foreach (var r in retracements.OrderBy(x => x.To.Time))
        {
            var setup = Identify(r);
            if (setup == null || (found.Count > 0 && (found[^1].End is not PricePoint end || end.Time >= setup.Identified.Time)))
                continue;
            found.Add(setup);
        }

        return found;

        RangeOutline? Identify(RetracementOutline r)
        {
            if (!trends.Any(t => t.Direction == r.Direction && t.Confirmed.Time <= r.To.Time && (t.End == null || t.End.Time > r.To.Time)))
                return null;

            var swing = r.Confirmed ? swings.FirstOrDefault(x => x.Direction == r.Direction && x.Start == r.To) : null;
            var limit = swing?.BreakOfStructure?.Time ?? DateTime.MaxValue;
            var against = r.Direction == EnumSwingDirection.Down ? EnumAnnotationType.BearishMarketStructureBreak : EnumAnnotationType.BullishMarketStructureBreak;

            foreach (var msb in finerBreaks.Where(b => b.Type == against && b.Reference.Time > r.To.Time && b.Break.Time < limit).OrderBy(b => b.Break.Time))
            {
                var deepest = r.Steps.LastOrDefault(s => s.Point.Time <= msb.Break.Time);
                if (deepest == null || deepest.Percent < minRetracement)
                    continue;
                if (deepest.Percent >= 100)
                    return null;

                var (low, high) = r.Direction == EnumSwingDirection.Down ? (r.To, deepest.Point) : (deepest.Point, r.To);
                var range = new RangeOutline(r.Level, r.Direction, r.From, low, high, msb.Break, deepest.Percent, band, null);
                for (var i = index[msb.Break.Time] + 1; i < prices.Count; i++)
                {
                    if (prices[i].Close > range.BandHigh || prices[i].Close < range.BandLow)
                        return range with { End = new PricePoint(prices[i].DateTime, prices[i].Close) };
                }

                return range;
            }

            return null;
        }
    }
}
