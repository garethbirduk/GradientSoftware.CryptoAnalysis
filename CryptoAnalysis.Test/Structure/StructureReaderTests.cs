using Gradient.CryptoAnalysis.Research;

namespace Gradient.CryptoAnalysis.Test.Structure;

/// <summary>
/// The reader has, after each candle, the market structure breaks, trends, retracements, candidates and ranges that the
/// sawtooth makes of the candles so far when it reads them all again.
/// </summary>
[TestClass]
public class StructureReaderTests
{
    // More levels than any series here has, so every level is read.
    private const int Depth = 60;

    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static string Lines(IEnumerable<string> lines) => string.Join("\n", lines.Order(StringComparer.Ordinal));

    // What the changes a reader reports add up to, candle by candle.
    private sealed class Held
    {
        public Dictionary<ReadBreak, int> Breaks { get; } = [];

        public Dictionary<ReadTrend, TrendState> Trends { get; } = [];

        public Dictionary<ReadRetracement, int> Retracements { get; } = [];

        public Dictionary<ReadCandidate, int> Candidates { get; } = [];

        public Dictionary<ReadRange, RangeState> Ranges { get; } = [];

        public void Take(StructureChanges changes, string at)
        {
            Take(Breaks, changes.Breaks.Select(x => (x.Break, x.Level)), at);
            Take(Trends, changes.Trends.Select(x => (x.Trend, x.State)), at);
            Take(Retracements, changes.Retracements.Select(x => (x.Retracement, x.Level)), at);
            Take(Candidates, changes.Candidates.Select(x => (x.Candidate, x.Level)), at);
            Take(Ranges, changes.Ranges.Select(x => (x.Range, x.State)), at);
        }

        private static void Take<TItem, TState>(Dictionary<TItem, TState> held, IEnumerable<(TItem Item, TState? State)> changes, string at)
            where TItem : notnull where TState : struct
        {
            foreach (var (item, state) in changes)
            {
                Assert.AreNotEqual(state, held.TryGetValue(item, out var was) ? was : (TState?)null, $"{at}: a change that changes nothing, of {item}.");
                if (state is { } now)
                    held[item] = now;
                else
                    held.Remove(item);
            }
        }
    }

    /// <summary>
    /// Reads the candles one at a time and, at each candle check says to, holds what the reader's changes add up to against
    /// the sawtooth of the candles to there.
    /// </summary>
    private static void AssertSame(List<Price> prices, EnumPriceBasis basis, Func<int, bool> check, string name,
        double minRetracement = Ranges.DefaultMinRetracement, double band = Ranges.DefaultBand)
    {
        var reader = new StructureReader(basis, minRetracement: minRetracement, band: band);
        var held = new Held();
        var ids = new HashSet<string>();
        var (ranges, trends, breaks) = (0, 0, 0);
        for (var i = 0; i < prices.Count; i++)
        {
            reader.Add(prices[i]);
            held.Take(reader.Changes, $"{name}, candle {i}");
            if (!check(i))
                continue;

            var so = prices.GetRange(0, i + 1);
            var times = so.Select((p, at) => (p.DateTime, at)).ToDictionary(x => x.DateTime, x => x.at);
            int? At(PricePoint? point) => point == null ? null : times[point.Time];
            var levels = Sawtooth.Levels(so, basis, Depth);
            var swings = Enumerable.Range(0, levels.Count + 2).Select(l => Sawtooth.Swings(so, levels, basis, l).OrderBy(x => x.Start.Time).ToList()).ToList();
            var broken = swings.Select(x => Sawtooth.MarketStructureBreaks(so, x, basis)).ToList();

            var expectedBreaks = new List<string>();
            var expectedTrends = new List<string>();
            var expectedRetracements = new List<string>();
            var expectedCandidates = new List<string>();
            var expectedRanges = new List<string>();
            for (var level = 1; level <= levels.Count; level++)
            {
                var ofLevel = Sawtooth.Trends(swings[level], broken[level]);
                var candidates = Sawtooth.Candidates(so, levels, basis, level);
                var retracements = Sawtooth.Retracements(so, swings[level], candidates, basis);
                expectedBreaks.AddRange(broken[level].Select(x => $"L{level} {x.Type} {At(x.Reference)} {At(x.Break)}"));
                expectedTrends.AddRange(ofLevel.Select(x => $"L{level} {x.Direction} {At(x.Start)} {At(x.Confirmed)} {At(x.End)} {x.Swings} {x.MarketStructureBreaks} "
                    + string.Join(" ", x.Parts.Select(p => $"{At(p.BreakOfStructure)}{(p.Strong ? "S" : "w")}"))));
                expectedRetracements.AddRange(retracements.Select(x => $"L{level} {x.Direction} {At(x.From)} {At(x.To)} {x.Confirmed} " + string.Join(" ", x.Steps.Select(s => At(s.Point)))));
                expectedCandidates.AddRange(candidates.Select(x => $"L{level} {x.Direction} {At(x.Start)} {At(x.Extreme)}"));
                expectedRanges.AddRange(Ranges.Find(so, basis, retracements, swings[level], ofLevel, broken[level + 1], minRetracement, band)
                    .Select(x => $"L{level} {x.Direction} {At(x.From)} {At(x.Low)} {At(x.High)} {At(x.Identified)} {x.Retracement} {x.Band} {At(x.End)}"));
            }

            Assert.AreEqual(Lines(expectedBreaks), Lines(held.Breaks.Select(x => $"L{x.Value} {x.Key.Type} {x.Key.Reference} {x.Key.Break}")),
                $"{name}: the market structure breaks at candle {i}.");
            Assert.AreEqual(Lines(expectedTrends), Lines(held.Trends.Select(x => $"L{x.Value.Level} {x.Key.Direction} {x.Key.First.Start} {x.Value.Confirmed} {x.Value.End} {x.Value.Swings} {x.Value.MarketStructureBreaks} "
                + string.Join(" ", x.Value.Parts.Select(p => $"{p.Break}{(p.Strong ? "S" : "w")}")))), $"{name}: the trends at candle {i}.");
            // A candidate's steps go on being added to, so those read are the ones up to this candle: all of them, here.
            Assert.AreEqual(Lines(expectedRetracements), Lines(held.Retracements.Select(x => $"L{x.Value} {x.Key.Direction} {x.Key.From} {x.Key.To} {x.Key.Confirmed} " + string.Join(" ", x.Key.Steps))),
                $"{name}: the retracements at candle {i}.");
            Assert.AreEqual(Lines(expectedCandidates), Lines(held.Candidates.Select(x => $"L{x.Value} {x.Key.Direction} {x.Key.Start} {x.Key.Steps[^1]}")),
                $"{name}: the candidates at candle {i}.");
            Assert.AreEqual(Lines(expectedRanges), Lines(held.Ranges.Select(x => $"L{x.Value.Level} {x.Key.Direction} {x.Key.From} {x.Key.Low} {x.Key.High} {x.Key.Identified} {x.Key.Retracement} {x.Key.Band} {x.Value.End}")),
                $"{name}: the ranges at candle {i}.");

            (ranges, trends, breaks) = (Math.Max(ranges, expectedRanges.Count), Math.Max(trends, expectedTrends.Count), Math.Max(breaks, expectedBreaks.Count));
        }

        // An id is of one item of its kind.
        foreach (var id in held.Breaks.Keys.Select(x => $"b{x.Id}").Concat(held.Trends.Keys.Select(x => $"t{x.Id}")).Concat(held.Retracements.Keys.Select(x => $"r{x.Id}"))
            .Concat(held.Candidates.Keys.Select(x => $"c{x.Id}")).Concat(held.Ranges.Keys.Select(x => $"g{x.Id}")))
        {
            Assert.IsTrue(ids.Add(id), $"{name}: the id {id} is of two items.");
        }

        Found = (Found.Ranges + ranges, Found.Trends + trends, Found.Breaks + breaks);
    }

    // How much the series of a test had to find, so that one finding nothing does not pass.
    private static (int Ranges, int Trends, int Breaks) Found;

    private static List<Price> FewPrices(int seed)
    {
        var random = new Random(seed);
        var close = (double)random.Next(1, 7);
        var prices = new List<Price>();
        for (var i = 0; i < 40; i++)
        {
            var open = close;
            close = Math.Clamp(close + random.Next(-2, 3), 1, 8);
            prices.Add(new Price { DateTime = Start.AddHours(i), Open = open, Close = close, High = Math.Max(open, close) + random.Next(0, 3), Low = Math.Min(open, close) - random.Next(0, 3) });
        }

        return prices;
    }

    [TestMethod]
    [DataRow(EnumPriceBasis.Close)]
    [DataRow(EnumPriceBasis.Wick)]
    public void ShortSeriesOfFewPrices_WithTheirTies_AreTheSawtoothAtEveryCandle(EnumPriceBasis basis)
    {
        Found = default;
        for (var seed = 0; seed < 400; seed++)
            AssertSame(FewPrices(seed), basis, _ => true, $"Seed {seed}", minRetracement: 40);

        Assert.IsTrue(Found is { Trends: > 100, Breaks: > 100 }, $"Too little to find: {Found}.");
    }

    [TestMethod]
    [DataRow(EnumPriceBasis.Close)]
    [DataRow(EnumPriceBasis.Wick)]
    public void RandomWalks_AreTheSawtoothAtEveryCandle(EnumPriceBasis basis)
    {
        Found = default;
        for (var seed = 1; seed <= 3; seed++)
            AssertSame(Synthetic.Walk(seed, Start, 1000), basis, _ => true, $"Walk {seed}");

        Assert.IsTrue(Found is { Ranges: > 3, Trends: > 30, Breaks: > 100 }, $"Too little to find: {Found}.");
    }

    [TestMethod]
    public void RandomWalks_WithALowerBarForRanges_AreTheSawtoothAtEveryCandle()
    {
        Found = default;
        AssertSame(Synthetic.Walk(4, Start, 1000), EnumPriceBasis.Close, _ => true, "Walk 4", minRetracement: 40, band: 5);

        Assert.IsTrue(Found.Ranges > 5, $"Too little to find: {Found}.");
    }

    [TestMethod]
    public void Btc_Through2023_IsTheSawtoothAtEveryCandle()
    {
        Found = default;
        AssertSame(Btc.From2023().GetRange(0, 2500), EnumPriceBasis.Close, _ => true, "BTC from 2023");

        Assert.IsTrue(Found is { Ranges: > 3, Trends: > 30, Breaks: > 100 }, $"Too little to find: {Found}.");
    }

    [TestMethod]
    public void Btc_From2020_IsTheSawtooth_AcrossTheHighOf2021BeingPassed()
    {
        var all = Btc.From2020();
        var high = Enumerable.Range(0, 30000).MaxBy(i => all[i].Close);
        var crossing = Enumerable.Range(30000, all.Count - 30000).First(i => all[i].Close > all[high].Close);

        // Every 4,000th candle, each candle either side of the close above the old high, and the last.
        AssertSame(all, EnumPriceBasis.Close, i => i % 4000 == 0 || Math.Abs(i - crossing) <= 5 || i == all.Count - 1, "BTC from 2020");
    }
}
