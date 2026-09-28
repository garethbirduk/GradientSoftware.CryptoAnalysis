namespace Gradient.CryptoAnalysis.Test.Structure;

[TestClass]
public class CandleRunsTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Builds candles from (open, close) pairs; highs and lows are irrelevant to runs.
    /// </summary>
    private static List<Price> Candles(params (double Open, double Close)[] candles) =>
        candles.Select((c, i) => new Price { DateTime = Start.AddHours(i), Open = c.Open, Close = c.Close, High = Math.Max(c.Open, c.Close), Low = Math.Min(c.Open, c.Close) }).ToList();

    private static string Describe(List<CandleRun> runs) =>
        string.Join(" ", runs.Select(r => $"{(r.Green ? "G" : "R")}{r.Length}:{r.Start.Price}>{r.End.Price}"));

    [TestMethod]
    public void Runs_AreMaximalAndAtLeastThree()
    {
        var prices = Candles((10, 11), (11, 12), (12, 13), (13, 12), (12, 13), (13, 14), (14, 15), (15, 16), (16, 15));

        Assert.AreEqual("G3:10>13 G4:12>16", Describe(CandleRuns.Runs(prices)));
    }

    [TestMethod]
    public void Runs_TwoCandlesAreNotARun()
    {
        var prices = Candles((10, 11), (11, 12), (12, 11), (11, 10));

        Assert.AreEqual("", Describe(CandleRuns.Runs(prices)));
    }

    [TestMethod]
    public void Runs_RedRunsAreFound()
    {
        var prices = Candles((20, 19), (19, 18), (18, 17), (17, 16), (16, 17));

        Assert.AreEqual("R4:20>16", Describe(CandleRuns.Runs(prices)));
    }

    [TestMethod]
    public void Runs_FlatCandleEndsARun()
    {
        var prices = Candles((10, 11), (11, 12), (12, 12), (12, 13), (13, 14), (14, 15));

        Assert.AreEqual("G3:12>15", Describe(CandleRuns.Runs(prices)));
    }

    [TestMethod]
    public void Runs_RunToTheLastCandleCounts()
    {
        var prices = Candles((10, 9), (9, 10), (10, 11), (11, 12));

        Assert.AreEqual("G3:9>12", Describe(CandleRuns.Runs(prices)));
    }

    [TestMethod]
    public void Runs_UseColourNotCloseToClose()
    {
        // The second candle gaps down but is still green, so the run continues.
        var prices = Candles((10, 11), (9, 10), (10, 11));

        Assert.AreEqual("G3:10>11", Describe(CandleRuns.Runs(prices)));
    }
}
