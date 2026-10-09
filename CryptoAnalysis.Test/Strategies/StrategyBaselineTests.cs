using Gradient.CryptoAnalysis.Strategies;

namespace Gradient.CryptoAnalysis.Test.Strategies;

[TestClass]
public class StrategyBaselineTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly Strategy GreenRun = new()
    {
        Id = "test",
        Entry = new StrategyEntry { Colour = EnumCandleColour.Green, Length = 4 },
        TakeProfit = new StrategyTarget { Type = StrategyTarget.Percent, Percentage = 1 },
        StopLoss = new StrategyTarget { Type = StrategyTarget.RiskRatio, Ratio = 1 },
    };

    // A random walk of candles, each moving up to 1.5% either way. With edge set, the candle after each run of four green
    // candles rises 2% from its open, so the strategy's Take Profit, 1% above, is always reached first.
    private static List<Price> Walk(int count, bool edge, int seed = 7)
    {
        var random = new Random(seed);
        var prices = new List<Price>();
        var close = 1000.0;
        for (var i = 0; i < count; i++)
        {
            var open = close;
            var rises = edge && prices.Count >= 4 && StrategyBacktest.Enters(prices, prices.Count - 1, GreenRun.Entry);
            close = rises ? open * 1.02 : open * (1 + (random.NextDouble() - 0.5) * 0.03);
            var high = Math.Max(open, close) * (1 + random.NextDouble() * 0.002);
            var low = Math.Min(open, close) * (1 - (rises ? 0 : random.NextDouble() * 0.002));
            prices.Add(new Price { DateTime = Start.AddHours(i), Open = open, High = high, Low = low, Close = close });
        }

        return prices;
    }

    private static BaselineMeasure Measure(BaselineRun run, string name) => run.Measures.Single(x => x.Name == name);

    [TestMethod]
    public void Entries_SameSeedSameCandles()
    {
        var first = StrategyBaseline.Entries(1000, 0.1, 3).Select(x => x.Index).ToList();
        var again = StrategyBaseline.Entries(1000, 0.1, 3).Select(x => x.Index).ToList();
        var other = StrategyBaseline.Entries(1000, 0.1, 4).Select(x => x.Index).ToList();

        CollectionAssert.AreEqual(first, again);
        CollectionAssert.AreNotEqual(first, other);
    }

    [TestMethod]
    public void Entries_ChanceSetsHowOftenACandleIsTried()
    {
        Assert.AreEqual(0, StrategyBaseline.Entries(1000, 0, 1).Count());
        Assert.AreEqual(1000, StrategyBaseline.Entries(1000, 1, 1).Count());
        Assert.AreEqual(100, StrategyBaseline.Entries(10000, 0.01, 1).Count(), 30);
    }

    [TestMethod]
    public void Quantile_FallsBetweenTheNearestValues()
    {
        double[] sorted = [10, 20, 30, 40, 50];

        Assert.AreEqual((10.0, 30.0, 50.0, 12.0), (StrategyBaseline.Quantile(sorted, 0), StrategyBaseline.Quantile(sorted, 50),
            StrategyBaseline.Quantile(sorted, 100), StrategyBaseline.Quantile(sorted, 5)));
    }

    [TestMethod]
    public void Run_SameSeedSameBaseline()
    {
        var prices = Walk(3000, edge: false);

        var first = StrategyBaseline.Run(prices, GreenRun, runs: 50, seed: 5);
        var again = StrategyBaseline.Run(prices, GreenRun, runs: 50, seed: 5);

        CollectionAssert.AreEqual(first.Measures, again.Measures);
    }

    [TestMethod]
    public void Run_RandomRunsTryAsOftenAsTheStrategy()
    {
        var prices = Walk(5000, edge: false);

        var run = StrategyBaseline.Run(prices, GreenRun, runs: 100);

        Assert.AreEqual(StrategyBacktest.Signals(prices, GreenRun).Count, run.Signals);
        Assert.AreEqual((double)run.Signals / prices.Count, run.Chance, 1e-12);
        // The exits are the same, so the trades the one-position rule lets through come out near the same too.
        Assert.AreEqual(run.Trades, run.AverageTrades, run.Trades * 0.25);
    }

    [TestMethod]
    public void Run_EntryWithAnEdgeBeatsTheRandomRuns()
    {
        var prices = Walk(5000, edge: true);

        var run = StrategyBaseline.Run(prices, GreenRun, runs: 200);

        var winRate = Measure(run, StrategyBaseline.WinRate);
        Assert.AreEqual(100.0, winRate.Strategy!.Value, 1e-9);
        Assert.IsTrue(winRate.Percentile > 99, $"Percentile {winRate.Percentile}");
        Assert.IsTrue(winRate.High < 70, $"Random win rates reach {winRate.High}");
    }

    [TestMethod]
    public void Run_EntryWithNoEdgeSitsAmongTheRandomRuns()
    {
        // One walk of 150 or so trades can land in a tail by chance, so the percentile is averaged over several.
        var percentiles = Enumerable.Range(1, 6)
            .Select(seed => Measure(StrategyBaseline.Run(Walk(5000, edge: false, seed), GreenRun, runs: 200), StrategyBaseline.WinRate).Percentile!.Value)
            .ToList();

        Assert.IsTrue(percentiles.Average() is > 20 and < 80, $"Percentiles {string.Join(", ", percentiles)}");
    }

    [TestMethod]
    public void CurvePoints_SpreadFromTheFirstCandleToTheLast()
    {
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, StrategyBaseline.CurvePoints(3));
        CollectionAssert.AreEqual(new[] { 0, 5, 10 }, StrategyBaseline.CurvePoints(11, most: 3));
        Assert.AreEqual(0, StrategyBaseline.CurvePoints(0).Count);
    }

    [TestMethod]
    public void Cumulative_AddsEachTradeAtItsExitAndLeavesOutOpenTrades()
    {
        StrategyTrade Trade(int exit, double percent, EnumTradeOutcome outcome = EnumTradeOutcome.TakeProfit) =>
            new(1, EnumTradeDirection.Long, 0, Start, 100, 0, 0, exit, Start, 0, outcome, percent, percent, exit, false, []);

        var sums = StrategyBaseline.Cumulative([Trade(4, 2), Trade(2, -1), Trade(9, 5, EnumTradeOutcome.Open)], [0, 2, 3, 4, 9]);

        CollectionAssert.AreEqual(new[] { 0.0, -1, -1, 1, 1 }, sums);
    }

    [TestMethod]
    public void Run_CurveEndsAtTheTotalsWithTheBandInOrder()
    {
        var prices = Walk(3000, edge: false);

        var run = StrategyBaseline.Run(prices, GreenRun, runs: 100);

        Assert.AreEqual(prices[^1].DateTime, run.Curve[^1].Time);
        Assert.AreEqual(Measure(run, StrategyBaseline.TotalProfitPercent).Strategy!.Value, run.Curve[^1].Strategy, 1e-9);
        Assert.AreEqual(Measure(run, StrategyBaseline.TotalProfitPercent).Median, run.Curve[^1].Median, 1e-9);
        Assert.IsTrue(run.Curve.All(x => x.Low <= x.Median && x.Median <= x.High));
    }

    [TestMethod]
    public void Run_StrategyThatNeverEntersHasNoRateToMatch()
    {
        var prices = Walk(3, edge: false);

        Assert.ThrowsException<ArgumentException>(() => StrategyBaseline.Run(prices, GreenRun, runs: 10));
    }
}
