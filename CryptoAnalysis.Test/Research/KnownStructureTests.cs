using Gradient.CryptoAnalysis.Research;
using Flag = Gradient.CryptoAnalysis.Research.KnownStructure.EnumFlag;

namespace Gradient.CryptoAnalysis.Test.Research;

[TestClass]
public class KnownStructureTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<Price> Closes(params double[] closes) =>
        closes.Select((c, i) => new Price { DateTime = Start.AddHours(i), Open = c, High = c, Low = c, Close = c }).ToList();

    private static string At(int[] masks, int level, Flag flag) =>
        string.Join(" ", masks.Select((m, i) => (m & (1 << KnownStructure.Bit(level, flag))) != 0 ? i : -1).Where(i => i >= 0));

    [TestMethod]
    public void Masks_AreTheReplaysEventsOnTheCandlesTheyBecomeKnown()
    {
        var masks = KnownStructure.Masks(Closes(10, 12, 11, 13, 12, 15), EnumPriceBasis.Close, 100, 2);

        Assert.AreEqual("3 5", At(masks, 1, Flag.BreakUp));
        Assert.AreEqual("5", At(masks, 1, Flag.Uptrend));
        Assert.AreEqual("", At(masks, 1, Flag.BreakDown));
    }

    [TestMethod]
    public void Masks_PresumedDeathLegDowntrendHoldsUntilTheNewHigh()
    {
        var masks = KnownStructure.Masks(Closes(10, 20, 17, 18, 15, 16, 14, 21), EnumPriceBasis.Close, 100, 1);

        Assert.AreEqual("4 6", At(masks, 1, Flag.BreakDown));
        Assert.AreEqual("6", At(masks, 1, Flag.Downtrend));
        Assert.AreEqual("7", At(masks, 1, Flag.BreakUp));
    }

    [TestMethod]
    public void Masks_SeeOnlyTheWindow()
    {
        Assert.AreEqual("", At(KnownStructure.Masks(Closes(10, 12, 11, 13, 12, 15), EnumPriceBasis.Close, 2, 1), 1, Flag.BreakUp));
    }

    [TestMethod]
    public void Masks_OfACandleDoNotChangeWithTheCandlesAfterIt()
    {
        var prices = Synthetic.Walk(3, Start, 900);

        var all = KnownStructure.Masks(prices, EnumPriceBasis.Close, 200, KnownStructure.Levels);
        var before = KnownStructure.Masks(prices.Take(600).ToList(), EnumPriceBasis.Close, 200, KnownStructure.Levels);

        CollectionAssert.AreEqual(before, all.Take(600).ToArray());
        Assert.IsTrue(all.Count(x => x != 0) > 300, "A random walk shows structure on most candles.");
    }
}
