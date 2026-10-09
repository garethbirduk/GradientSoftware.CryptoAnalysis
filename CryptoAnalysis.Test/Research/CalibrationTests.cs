using Gradient.CryptoAnalysis.Research;
using Gradient.CryptoAnalysis.Strategies;

namespace Gradient.CryptoAnalysis.Test.Research;

/// <summary>
/// Checks that the way strategies are judged tells an edge from luck, on synthetic markets where the truth is known: on random
/// walks a strategy should beat 95% of its random runs about one time in twenty, no more, and a sweep's best only as often
/// once set against the best of the random runs; a planted edge should be found. The seeds are fixed, so the counts are the
/// same each run. Scaled down to run quickly: a fuller run (100 seeds of a year, 1,000 random runs) gave 5% for one strategy,
/// 16% for a sweep's best of 11 against its own random runs, 4% against the best of 11, and 99% of planted edges found.
/// </summary>
[TestClass]
[TestCategory("Calibration")]
public class CalibrationTests
{
    private static readonly DateTime Start = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // After 3 green candles, a Take Profit a percentage above the entry and a Stop Loss as far below, with no costs.
    private static Strategy ThreeGreen(double takeProfit) => new()
    {
        Id = "calibration",
        Entry = new StrategyEntry { Colour = EnumCandleColour.Green, Length = 3 },
        TakeProfit = new StrategyTarget { Type = StrategyTarget.Percent, Percentage = takeProfit },
        StopLoss = new StrategyTarget { Type = StrategyTarget.RiskRatio, Ratio = 1 },
    };

    private static double? Beats(BaselineRun run) => run.Measures.Single(x => x.Name == StrategyBaseline.TotalProfitPercent).Percentile;

    [TestMethod]
    public void RandomWalk_OneStrategyBeatsRandomAboutOneTimeInTwenty()
    {
        var hits = Enumerable.Range(1, 40).Count(seed => Beats(StrategyBaseline.Run(Synthetic.Walk(seed, Start, 8760), ThreeGreen(2), runs: 200)) >= 95);

        // 2 expected of 40; 6 or more would happen by chance under 2% of the time.
        Assert.IsTrue(hits <= 5, $"{hits} of 40 random walks beat 95% of their random runs.");
    }

    [TestMethod]
    public void RandomWalk_ASweepsBestIsOnlyFairlyJudgedAgainstTheBestOfTheRandomRuns()
    {
        var (own, best) = (0, 0);
        foreach (var seed in Enumerable.Range(1, 30))
        {
            var strategy = ThreeGreen(1);
            strategy.Vary = new() { ["takeProfit.percent"] = new StrategyRange { To = 3, Step = 0.5 } };
            var sweep = StrategySweep.Run(Synthetic.Walk(seed, Start, 4380), strategy, runs: 200);
            var top = sweep.Best.Single(x => x.Against.Name == StrategyBaseline.TotalProfitPercent);
            if (Beats(sweep.Variations[top.Variation!.Value].Baseline!) >= 95)
                own++;
            if (top.Against.Percentile >= 95)
                best++;
        }

        Assert.IsTrue(own > best, $"Against its own random runs {own} of 30 bests beat 95%, against the best of the random runs {best}.");
        Assert.IsTrue(best <= 4, $"{best} of 30 sweeps' bests beat 95% of the best of the random runs.");
    }

    [TestMethod]
    public void PlantedEdge_IsFound()
    {
        var hits = Enumerable.Range(1, 10).Count(seed => Beats(StrategyBaseline.Run(Synthetic.PlantedEdge(seed, Start, 8760), ThreeGreen(2), runs: 200)) >= 95);

        Assert.IsTrue(hits >= 9, $"Only {hits} of 10 planted edges beat 95% of their random runs.");
    }

    [TestMethod]
    public void Walk_IsTheSameForASeedAndHourly()
    {
        var a = Synthetic.Walk(7, Start, 100);
        var b = Synthetic.Walk(7, Start, 100);

        CollectionAssert.AreEqual(a.Select(x => x.Close).ToList(), b.Select(x => x.Close).ToList());
        Assert.IsTrue(a.Zip(a.Skip(1), (x, y) => y.DateTime - x.DateTime).All(x => x == TimeSpan.FromHours(1)));
        Assert.IsTrue(a.All(x => x.Low <= Math.Min(x.Open, x.Close) && x.High >= Math.Max(x.Open, x.Close)));
    }
}
