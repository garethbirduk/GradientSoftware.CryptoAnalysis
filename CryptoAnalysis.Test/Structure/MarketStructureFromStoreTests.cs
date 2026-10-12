using System.Text.Json;
using Gradient.CryptoAnalysis.Research;

namespace Gradient.CryptoAnalysis.Test.Structure;

/// <summary>
/// The replay's timeline looked up in the structure store is the one worked out as the candles are read.
/// </summary>
[TestClass]
public class MarketStructureFromStoreTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // The entries of a kind, each as JSON, in an order of their own: the order two found on a candle come in is not held to.
    private static string Entries<T>(List<ReplayEntry<T>> entries) => string.Join("\n", entries.Select(x => JsonSerializer.Serialize(x)).Order(StringComparer.Ordinal));

    private static void AssertSame(List<Price> prices, StructureStore store, int from, int? to, int? keepFrom, string name,
        double minRetracement = Ranges.DefaultMinRetracement, double band = Ranges.DefaultBand)
    {
        var expected = MarketStructure.Timeline(prices, store.Basis, 8, from: from, to: to, keepFrom: keepFrom, minRetracement: minRetracement, band: band);
        var timeline = MarketStructure.Timeline(store, 8, from, to, keepFrom);
        Assert.AreEqual(Entries(expected.Pivots), Entries(timeline.Pivots), $"{name}: the pivots.");
        Assert.AreEqual(Entries(expected.Swings), Entries(timeline.Swings), $"{name}: the swings.");
        Assert.AreEqual(Entries(expected.Candidates), Entries(timeline.Candidates), $"{name}: the candidates.");
        Assert.AreEqual(Entries(expected.MarketStructureBreaks), Entries(timeline.MarketStructureBreaks), $"{name}: the market structure breaks.");
        Assert.AreEqual(Entries(expected.Trends), Entries(timeline.Trends), $"{name}: the trends.");
        Assert.AreEqual(Entries(expected.Points), Entries(timeline.Points), $"{name}: the points.");
        Assert.AreEqual(Entries(expected.CandleRuns), Entries(timeline.CandleRuns), $"{name}: the candle runs.");
        Assert.AreEqual(Entries(expected.Retracements), Entries(timeline.Retracements), $"{name}: the retracements.");
        Assert.AreEqual(Entries(expected.Ranges), Entries(timeline.Ranges), $"{name}: the ranges.");
    }

    // A batch as the server asks for one: the candles from start, the one before them, and what a month's window shows.
    private static void AssertBatch(List<Price> prices, StructureStore store, int start, string name) =>
        AssertSame(prices, store, Math.Max(0, start - 1), Math.Min(start + 50, prices.Count), Math.Max(0, start - 744), $"{name}, the batch from {start}");

    [TestMethod]
    [DataRow(EnumPriceBasis.Close)]
    [DataRow(EnumPriceBasis.Wick)]
    public void RandomWalks_EveryCandleReplayed(EnumPriceBasis basis)
    {
        for (var seed = 1; seed <= 3; seed++)
        {
            var prices = Synthetic.Walk(seed, Start, 500);
            AssertSame(prices, StructureStore.Read(prices, basis), 0, null, null, $"Walk {seed}");
        }
    }

    [TestMethod]
    public void RandomWalks_AWindowKept_AndALowerBarForRanges()
    {
        var prices = Synthetic.Walk(4, Start, 700);

        AssertSame(prices, StructureStore.Read(prices), 399, 500, 300, "A window");
        AssertSame(prices, StructureStore.Read(prices, EnumPriceBasis.Wick), 399, 500, 300, "A window, on wicks");
        AssertSame(prices, StructureStore.Read(prices, minRetracement: 40, band: 5), 0, null, null, "A lower bar", minRetracement: 40, band: 5);
    }

    [TestMethod]
    public void Btc_BatchesThrough2023_AndTheWholeAsKnownAtItsEnd()
    {
        var prices = Btc.From2023().GetRange(0, 3000);
        var store = StructureStore.Read(prices);

        foreach (var start in new[] { 0, 50, 700, 750, 1500, 2200, 2950 })
            AssertBatch(prices, store, start, "BTC from 2023");
        AssertSame(prices, store, prices.Count - 1, prices.Count, 0, "BTC from 2023, as the Chart page asks");
    }

    [TestMethod]
    public void Btc_BatchesDeepInFrom2020_AndAcrossTheHighOf2021BeingPassed()
    {
        var all = Btc.From2020();
        var store = StructureStore.Read(all);
        var high = Enumerable.Range(0, 30000).MaxBy(i => all[i].Close);
        var crossing = Enumerable.Range(30000, all.Count - 30000).First(i => all[i].Close > all[high].Close);

        AssertBatch(all, store, 20500, "BTC from 2020");
        AssertBatch(all, store, crossing - 25, "BTC from 2020");
    }

    [TestMethod]
    public void ALevelAsKnownAtACandle_IsWhatTheSawtoothMakesOfTheCandlesToIt()
    {
        const EnumPriceBasis basis = EnumPriceBasis.Close;
        var prices = Btc.From2023().GetRange(0, 3000);
        var store = StructureStore.Read(prices);
        string Json<T>(T value) => JsonSerializer.Serialize(value);

        foreach (var at in new[] { 0, 1, 2, 57, 500, 501, 1234, 2000, 2999 })
        {
            var so = prices.GetRange(0, at + 1);
            var levels = Sawtooth.Levels(so, basis, 60);
            for (var level = 1; level <= levels.Count; level++)
            {
                Assert.AreEqual(level < levels.Count, MarketStructure.HasLevel(store, level, at), $"Whether there is a level {level} at candle {at}.");
                var swings = Sawtooth.Swings(so, levels, basis, level).OrderBy(x => x.Start.Time).ToList();
                var breaks = Sawtooth.MarketStructureBreaks(so, swings, basis);
                Assert.AreEqual(Json(swings), Json(MarketStructure.SwingsAt(store, level, at)), $"The swings of level {level} at candle {at}.");
                Assert.AreEqual(Json(breaks), Json(MarketStructure.BreaksAt(store, level, at)), $"The market structure breaks of level {level} at candle {at}.");
                Assert.AreEqual(Json(Sawtooth.Trends(swings, breaks)), Json(MarketStructure.TrendsAt(store, level, at)), $"The trends of level {level} at candle {at}.");
                var points = level < levels.Count ? Sawtooth.Points(so, levels[level], basis) : [];
                Assert.AreEqual(Json(points), Json(MarketStructure.PointsAt(store, level, at)), $"The points of level {level} at candle {at}.");
                var from = Math.Max(0, at - 100);
                Assert.AreEqual(Json(points.Where(x => x.Time >= prices[from].DateTime)), Json(MarketStructure.PointsAt(store, level, at, from)), $"The points of level {level} at candle {at}, from {from}.");
            }
        }
    }

    [TestMethod]
    public void AStretchOfTheWhole_IsWhatOfTheWholeLiesInIt_AndLeavesOutWhatIsTooNarrow()
    {
        var prices = Btc.From2023().GetRange(0, 3000);
        var store = StructureStore.Read(prices);
        var (from, to) = (prices[1000].DateTime, prices[1500].DateTime);
        var whole = MarketStructure.Timeline(store, 8, 2999, 3000, 0);
        HashSet<string> Set<T>(List<ReplayEntry<T>> entries) => entries.Select(x => JsonSerializer.Serialize(x)).ToHashSet();
        string Lines<T>(IEnumerable<ReplayEntry<T>> entries) => string.Join("\n", entries.Select(x => JsonSerializer.Serialize(x)).Order(StringComparer.Ordinal));

        var all = MarketStructure.Complete(store);
        Assert.AreEqual(Entries(whole.Swings), Entries(all.Swings));
        Assert.AreEqual(Entries(whole.Pivots), Entries(all.Pivots));
        Assert.AreEqual(Entries(whole.Points), Entries(all.Points));
        Assert.AreEqual(Entries(whole.Retracements), Entries(all.Retracements));
        Assert.AreEqual(Entries(whole.CandleRuns), Entries(all.CandleRuns));

        var stretch = MarketStructure.Complete(store, 8, 1000, 1500);
        Assert.AreEqual(Lines(whole.Swings.Where(x => x.Value.End.Time >= from && x.Value.Start.Time <= to)), Entries(stretch.Swings));
        Assert.AreEqual(Lines(whole.Points.Where(x => x.Value.Time >= from && x.Value.Time <= to)), Entries(stretch.Points));
        Assert.AreEqual(Lines(whole.MarketStructureBreaks.Where(x => x.Value.Break.Break.Time >= from && x.Value.Break.Break.Time <= to)), Entries(stretch.MarketStructureBreaks));
        Assert.AreEqual(Lines(whole.Trends.Where(x => (x.Value.End == null || x.Value.End.Time >= from) && x.Value.Start.Time <= to)), Entries(stretch.Trends));
        Assert.AreEqual(Lines(whole.CandleRuns.Where(x => x.Value.End.Time >= from && x.Value.Start.Time <= to)), Entries(stretch.CandleRuns));
        Assert.IsTrue(Set(stretch.Retracements).IsSubsetOf(Set(whole.Retracements)) && Set(stretch.Ranges).IsSubsetOf(Set(whole.Ranges)) && Set(stretch.Pivots).IsSubsetOf(Set(whole.Pivots)));
        // Each level's line runs from the pivot before the stretch to the one after it.
        foreach (var level in stretch.Pivots.GroupBy(x => x.Value.Level))
        {
            var ofWhole = whole.Pivots.Where(x => x.Value.Level == level.Key).Select(x => x.Value.Pivot.Index).Order().ToList();
            var first = ofWhole.LastOrDefault(x => x < 1000, ofWhole[0]);
            var last = ofWhole.FirstOrDefault(x => x > 1500, ofWhole[^1]);
            Assert.AreEqual(string.Join(" ", ofWhole.Where(x => x >= first && x <= last)), string.Join(" ", level.Select(x => x.Value.Pivot.Index).Order()), $"The pivots of level {level.Key}.");
        }

        var wide = MarketStructure.Complete(store, 8, 0, null, 40);
        var candle = prices.Select((p, i) => (p.DateTime, i)).ToDictionary(x => x.DateTime, x => x.i);
        Assert.AreEqual(Lines(whole.Swings.Where(x => candle[x.Value.End.Time] - candle[x.Value.Start.Time] >= 40)), Entries(wide.Swings));
        Assert.IsTrue(wide.CandleRuns.Count == 0 && wide.Points.Count > 0 && wide.Points.Count < whole.Points.Count / 4 && wide.Pivots.Count < whole.Pivots.Count / 4);
        Assert.IsTrue(Set(wide.Points).IsSubsetOf(Set(whole.Points)) && Set(wide.Pivots).IsSubsetOf(Set(whole.Pivots)) && Set(wide.Trends).IsSubsetOf(Set(whole.Trends)));
    }
}