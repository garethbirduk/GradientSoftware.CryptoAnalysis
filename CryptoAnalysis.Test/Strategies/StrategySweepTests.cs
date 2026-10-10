using System.Text.Json;
using Gradient.CryptoAnalysis.Strategies;
using static Gradient.CryptoAnalysis.Test.Strategies.StrategyBaselineTests;

namespace Gradient.CryptoAnalysis.Test.Strategies;

[TestClass]
public class StrategySweepTests
{
    // The baseline tests' strategy, as its own copy, varying the given numbers.
    private static Strategy Varying(params (string Path, double To, double Step)[] vary)
    {
        var strategy = JsonSerializer.Deserialize<Strategy>(JsonSerializer.Serialize(GreenRun, StrategyBook.JsonOptions), StrategyBook.JsonOptions)!;
        strategy.Vary = vary.Length > 0 ? vary.ToDictionary(x => x.Path, x => new StrategyRange { To = x.To, Step = x.Step }) : null;
        return strategy;
    }

    [TestMethod]
    public void Variations_NothingVariedIsTheStrategyAlone()
    {
        var variations = StrategySweep.Variations(Varying());

        Assert.AreEqual(1, variations.Count);
        Assert.AreEqual(0, variations[0].Values.Count);
    }

    [TestMethod]
    public void Variations_RunFromTheStrategysValueToTheEndInSteps()
    {
        var strategy = Varying(("stopLoss.ratio", 2.0, 0.1));
        strategy.StopLoss.Ratio = 0.5;

        var variations = StrategySweep.Variations(strategy);

        Assert.AreEqual(16, variations.Count);
        CollectionAssert.AreEqual(new[] { 0.5, 0.6, 0.7 }, variations.Take(3).Select(x => x.Strategy.StopLoss.Ratio).ToList());
        Assert.AreEqual(2.0, variations[^1].Values["stopLoss.ratio"]);
        Assert.IsTrue(variations.All(x => x.Strategy.Vary == null));
    }

    [TestMethod]
    public void Variations_EveryCombinationTheLastVariedFastest()
    {
        var variations = StrategySweep.Variations(Varying(("entry.length", 5, 1), ("stopLoss.ratio", 1.5, 0.5)));

        CollectionAssert.AreEqual(new[] { (4, 1.0), (4, 1.5), (5, 1.0), (5, 1.5) },
            variations.Select(x => (x.Strategy.Entry.Length, x.Strategy.StopLoss.Ratio)).ToList());
    }

    [TestMethod]
    public void Variations_WholeNumberWithAStepThatIsNotWholeIsRefused()
    {
        var e = Assert.ThrowsException<ArgumentException>(() => StrategySweep.Variations(Varying(("entry.length", 6, 0.5))));

        StringAssert.Contains(e.Message, "whole");
    }

    [TestMethod]
    public void Variations_RangesThatCannotBeRunAreRefused()
    {
        Assert.ThrowsException<ArgumentException>(() => StrategySweep.Variations(Varying(("entry.colour", 2, 1))));
        Assert.ThrowsException<ArgumentException>(() => StrategySweep.Variations(Varying(("stopLoss.nothing", 2, 1))));
        Assert.ThrowsException<ArgumentException>(() => StrategySweep.Variations(Varying(("stopLoss.ratio", 0.5, 0.1))));
        Assert.ThrowsException<ArgumentException>(() => StrategySweep.Variations(Varying(("stopLoss.ratio", 2, 0))));
        Assert.ThrowsException<ArgumentException>(() => StrategySweep.Variations(Varying(("entry.length", 100, 1), ("takeProfit.percent", 100, 1))));
    }

    [TestMethod]
    public void Run_EachVariationHasItsOwnBaseline()
    {
        var prices = Walk(3000, edge: false);

        var sweep = StrategySweep.Run(prices, Varying(("stopLoss.ratio", 2, 0.5)), runs: 50);

        CollectionAssert.AreEqual(new[] { 1.0, 1.5, 2.0 }, sweep.Variations.Select(x => x.Baseline!.Strategy.StopLoss.Ratio).ToList());
        CollectionAssert.AreEqual(new[] { "stopLoss.ratio" }, sweep.Varied);
    }

    [TestMethod]
    public void Run_VariationThatCannotRunSaysWhyAndTheRestRun()
    {
        var prices = Walk(3000, edge: false);
        var strategy = Varying(("entry.length", 40, 36));

        var sweep = StrategySweep.Run(prices, strategy, runs: 20);

        Assert.IsNotNull(sweep.Variations[0].Baseline);
        Assert.IsNull(sweep.Variations[1].Baseline);
        StringAssert.Contains(sweep.Variations[1].Error, "not met anywhere");
    }

    [TestMethod]
    public void Run_BestIsSetAgainstTheBestOfTheRandomRuns()
    {
        var prices = Walk(5000, edge: false);

        var sweep = StrategySweep.Run(prices, Varying(("takeProfit.percent", 3, 0.25)), runs: 200);

        var best = sweep.Best.Single(x => x.Against.Name == StrategyBaseline.TotalProfitPercent);
        var variation = sweep.Variations[best.Variation!.Value].Baseline!;
        var alone = variation.Measures.Single(x => x.Name == StrategyBaseline.TotalProfitPercent);
        Assert.AreEqual(sweep.Variations.Max(x => x.Baseline!.Measures.Single(m => m.Name == StrategyBaseline.TotalProfitPercent).Strategy), best.Against.Strategy);
        // The best of nine is harder to beat than one variation's random runs, so the best looks less special against it.
        Assert.IsTrue(best.Against.Median > alone.Median, $"Best-of median {best.Against.Median}, one variation's {alone.Median}");
        Assert.IsTrue(best.Against.Percentile <= alone.Percentile, $"{best.Against.Percentile} against {alone.Percentile}");
    }

    [TestMethod]
    public void Run_EdgeStillBeatsTheBestOfTheRandomRuns()
    {
        var prices = Walk(5000, edge: true);

        var sweep = StrategySweep.Run(prices, Varying(("stopLoss.ratio", 1.5, 0.25)), runs: 200);

        var best = sweep.Best.Single(x => x.Against.Name == StrategyBaseline.WinRate);
        Assert.IsTrue(best.Against.Percentile > 99, $"Percentile {best.Against.Percentile}");
    }
}
