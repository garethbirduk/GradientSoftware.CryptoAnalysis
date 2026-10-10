using Gradient.CryptoAnalysis.Research;

namespace Gradient.CryptoAnalysis.Test.Research;

[TestClass]
public class ExplorerTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<Price> Candles(params (double Open, double High, double Low, double Close)[] candles) =>
        candles.Select((c, i) => new Price { DateTime = Start.AddHours(i), Open = c.Open, High = c.High, Low = c.Low, Close = c.Close }).ToList();

    [TestMethod]
    public void FirstTouch_IsTheSideReachedFirst()
    {
        // From 100, 1% either way: up at 101, down at 99.
        var prices = Candles((100, 100, 100, 100), (100, 100.5, 99.5, 100), (100, 101.2, 99.8, 101), (101, 102, 100, 101.5));

        var touches = Explorer.FirstTouch(prices, 1, 5);

        Assert.AreEqual((EnumFirstTouch.Up, 2), touches[0]);
        Assert.AreEqual((EnumFirstTouch.Neither, 3), touches[2]);
    }

    [TestMethod]
    public void FirstTouch_CandleReachingBothIsBothAndAGapIsItsSide()
    {
        var prices = Candles((100, 100, 100, 100), (100, 102, 98, 100), (97, 97, 96, 96.5));

        var touches = Explorer.FirstTouch(prices, 1, 5);

        Assert.AreEqual((EnumFirstTouch.Both, 1), touches[0]);
        Assert.AreEqual((EnumFirstTouch.Down, 2), touches[1]);
    }

    [TestMethod]
    public void FirstTouch_NotWithinTheHorizonIsNeither()
    {
        var prices = Candles((100, 100, 100, 100), (100, 100.2, 99.9, 100), (100, 100.2, 99.9, 100), (100, 105, 100, 105));

        Assert.AreEqual((EnumFirstTouch.Neither, 2), Explorer.FirstTouch(prices, 1, 2)[0]);
        Assert.AreEqual((EnumFirstTouch.Up, 3), Explorer.FirstTouch(prices, 1, 3)[0]);
    }

    [TestMethod]
    public void Measure_CountsOneEventAtATimeAgainstEveryCandle()
    {
        bool[] met = [true, true, false, true, false];
        (EnumFirstTouch, int)[] touches = [(EnumFirstTouch.Up, 2), (EnumFirstTouch.Down, 3), (EnumFirstTouch.Down, 4), (EnumFirstTouch.Up, 4), (EnumFirstTouch.Neither, 4)];

        var stat = Explorer.Measure(met, touches, (0, 5));

        // The event at 1 is met while the one at 0 is still open, so it is not counted.
        Assert.AreEqual((2, 2, 0, 0, 0), (stat.Events, stat.Up, stat.Down, stat.Neither, stat.Both));
        Assert.AreEqual((2, 2), (stat.BaseUp, stat.BaseDown));
        Assert.AreEqual((1.0, 0.5), (stat.UpRate, stat.BaseRate));
    }

    [TestMethod]
    public void Run_IsMetOnceARunAtItsLength()
    {
        var prices = Candles((1, 2, 1, 2), (2, 3, 2, 3), (3, 4, 3, 4), (4, 5, 4, 5), (5, 5, 3, 4), (4, 5, 4, 5), (5, 6, 5, 6));

        CollectionAssert.AreEqual(new[] { false, false, true, false, false, false, false }, ExploreConditions.Run(prices, green: true, 3));
        CollectionAssert.AreEqual(new[] { false, true, false, false, false, false, true }, ExploreConditions.Run(prices, green: true, 2));
    }

    [TestMethod]
    public void Beyond_AgainstAverageAndSize_LookOnlyBackwards()
    {
        var prices = Candles((10, 11, 9, 10), (10, 11, 9, 10), (10, 15, 10, 14), (14, 14.5, 13.5, 14));

        CollectionAssert.AreEqual(new[] { false, false, true, false }, ExploreConditions.Beyond(prices, 2, high: true));
        // The average takes in the candle itself: at the last, (14 + 14) / 2 is its close, which is not above it.
        CollectionAssert.AreEqual(new[] { false, false, true, false }, ExploreConditions.AgainstAverage(prices, 2, above: true));
        CollectionAssert.AreEqual(new[] { false, false, true, false }, ExploreConditions.Size(prices, 2, larger: true));
        CollectionAssert.AreEqual(new[] { false, false, false, true }, ExploreConditions.Size(prices, 2, larger: false));
    }

    [TestMethod]
    public void NormalCdfAndCorrected_AreTheKnownValues()
    {
        Assert.AreEqual(0.5, Explorer.NormalCdf(0), 1e-7);
        Assert.AreEqual(0.975, Explorer.NormalCdf(1.959964), 1e-6);
        Assert.AreEqual(0.025, Explorer.NormalCdf(-1.959964), 1e-6);
        Assert.AreEqual(0.05, Explorer.Corrected(97.5, 1), 1e-9);
        Assert.AreEqual(1 - Math.Pow(0.95, 10), Explorer.Corrected(2.5, 10), 1e-9);
    }

    [TestMethod]
    [TestCategory("Calibration")]
    public void RandomWalks_SeldomShowAConditionBeyondChanceOnceEveryTestIsAllowedFor()
    {
        double[] sizes = [0.5, 1, 2];
        var tests = ExploreConditions.All.Count * sizes.Length;
        var flagged = Enumerable.Range(1, 20).Count(seed =>
        {
            var prices = Synthetic.Walk(seed, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), 8760);
            return sizes.Any(size =>
            {
                var touches = Explorer.FirstTouch(prices, size, 48);
                return ExploreConditions.All.Any(c => Explorer.Corrected(Explorer.Measure(c.Test(prices), touches, (0, prices.Count)).Score, tests) < 0.05);
            });
        });

        // One in 20 expected, as every test is allowed for; 4 or more would happen by chance under 2% of the time.
        Assert.IsTrue(flagged <= 3, $"{flagged} of 20 random walks had a condition beyond chance.");
    }

    [TestMethod]
    [TestCategory("Calibration")]
    public void PlantedEdge_IsFoundForItsCondition()
    {
        var prices = Synthetic.PlantedEdge(1, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), 8760);

        var stat = Explorer.Measure(ExploreConditions.All.Single(x => x.Id == "green-3").Test(prices), Explorer.FirstTouch(prices, 0.5, 48), (0, prices.Count));

        Assert.IsTrue(stat.Score > 99.9, $"Score {stat.Score} for the planted edge's condition.");
        Assert.IsTrue(Explorer.Corrected(stat.Score, 66) < 0.05);
    }
}
