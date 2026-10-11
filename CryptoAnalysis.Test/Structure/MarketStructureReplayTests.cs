using System.Text.Json;
using Gradient.CryptoAnalysis.Research;

namespace Gradient.CryptoAnalysis.Test.Structure;

/// <summary>
/// The replay's timeline, taken as the candles are read once, is the one worked out the plain way, reading every candle
/// again at each candle replayed.
/// </summary>
[TestClass]
public class MarketStructureReplayTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static void AssertSame(List<Price> prices, EnumPriceBasis basis, int from, int? to, int? keepFrom, string name, double minRetracement = Ranges.DefaultMinRetracement, double band = Ranges.DefaultBand)
    {
        var expected = MarketStructure.TimelineReadingAgain(prices, basis, 8, from: from, to: to, keepFrom: keepFrom, minRetracement: minRetracement, band: band);
        var timeline = MarketStructure.Timeline(prices, basis, 8, from: from, to: to, keepFrom: keepFrom, minRetracement: minRetracement, band: band);
        foreach (var (kind, of) in new (string, Func<ReplayTimeline, object>)[]
        {
            ("pivots", x => x.Pivots), ("swings", x => x.Swings), ("candidates", x => x.Candidates), ("market structure breaks", x => x.MarketStructureBreaks),
            ("trends", x => x.Trends), ("points", x => x.Points), ("candle runs", x => x.CandleRuns), ("retracements", x => x.Retracements), ("ranges", x => x.Ranges),
        })
        {
            Assert.AreEqual(JsonSerializer.Serialize(of(expected)), JsonSerializer.Serialize(of(timeline)), $"{name}: the {kind}.");
        }
    }

    // A batch as the server asks for one: the candles from start, the one before them, and what a month's window shows.
    private static void AssertBatch(List<Price> prices, int start, string name) =>
        AssertSame(prices, EnumPriceBasis.Close, Math.Max(0, start - 1), Math.Min(start + 50, prices.Count), Math.Max(0, start - 744), $"{name}, the batch from {start}");

    [TestMethod]
    [DataRow(EnumPriceBasis.Close)]
    [DataRow(EnumPriceBasis.Wick)]
    public void RandomWalks_EveryCandleReplayed(EnumPriceBasis basis)
    {
        for (var seed = 1; seed <= 3; seed++)
            AssertSame(Synthetic.Walk(seed, Start, 500), basis, 0, null, null, $"Walk {seed}");
    }

    [TestMethod]
    public void RandomWalks_AWindowKept_AndALowerBarForRanges()
    {
        var prices = Synthetic.Walk(4, Start, 700);

        AssertSame(prices, EnumPriceBasis.Close, 399, 500, 300, "A window");
        AssertSame(prices, EnumPriceBasis.Wick, 399, 500, 300, "A window, on wicks");
        AssertSame(prices, EnumPriceBasis.Close, 0, null, null, "A lower bar", minRetracement: 40, band: 5);
    }

    [TestMethod]
    public void Btc_BatchesThrough2023()
    {
        var prices = Btc.From2023().GetRange(0, 3000);

        foreach (var start in new[] { 0, 50, 700, 750, 1500, 2200, 2950 })
            AssertBatch(prices, start, "BTC from 2023");
    }

    [TestMethod]
    public void Btc_TheWholeOfAStretchAsKnownAtItsEnd()
    {
        var prices = Btc.From2023().GetRange(0, 3000);

        AssertSame(prices, EnumPriceBasis.Close, prices.Count - 1, prices.Count, 0, "BTC from 2023, as the Chart page asks");
    }

    [TestMethod]
    public void Btc_BatchesDeepInFrom2020_AndAcrossTheHighOf2021BeingPassed()
    {
        var all = Btc.From2020();
        var high = Enumerable.Range(0, 30000).MaxBy(i => all[i].Close);
        var crossing = Enumerable.Range(30000, all.Count - 30000).First(i => all[i].Close > all[high].Close);

        AssertBatch(all, 20500, "BTC from 2020");
        AssertBatch(all, crossing - 25, "BTC from 2020");
    }
}