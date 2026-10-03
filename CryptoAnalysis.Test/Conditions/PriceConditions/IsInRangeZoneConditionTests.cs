using CryptoAnalysis.Conditions;
using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Conditions;
using Gradient.CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis.Test.Conditions.PriceConditions;

/// <summary>
/// The range conditions on the training's own example (TestData/Terms/Range/04-the-training-example.csv, 4-hour candles
/// from the all-time high of 10 Nov 2021): the range 55,927 to 59,761 identified on 22 Nov 00:00, bands at 55,160 and
/// 60,527, zones 25% deep, ending below the band on 26 Nov 08:00.
/// </summary>
[TestClass]
public class IsInRangeZoneConditionTests
{
    private static readonly string DataPath = Path.Combine("TestData", "Terms", "Range", "04-the-training-example.csv");
    private const int Candles = 500;
    private List<Price> _prices = [];

    [TestInitialize]
    public void TestInitialize()
    {
        _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(DataPath).ToList();
    }

    private static ConditionSet Set(ICondition condition)
    {
        var set = new ConditionSet();
        set.AndConditions.Add(condition);
        return set;
    }

    private List<DateTime> MetAt(ICondition condition)
    {
        var set = Set(condition);
        return _prices.Where(x => set.IsMet(_prices, x.DateTime)).Select(x => x.DateTime).ToList();
    }

    [TestMethod]
    public void RangeHolds_FromIdentificationToEnd()
    {
        var met = MetAt(new IsRangeHoldingCondition(additionalCandles: Candles));

        Assert.AreEqual(new DateTime(2021, 11, 22, 0, 0, 0), met.First());
        Assert.AreEqual(new DateTime(2021, 11, 26, 4, 0, 0), met.Last());
        Assert.AreEqual(26, met.Count, "every 4-hour candle from identification to the one before the end");
    }

    [TestMethod]
    public void RangeEnd_IsTheCloseBelowTheBand()
    {
        CollectionAssert.AreEqual(new List<DateTime> { new(2021, 11, 26, 8, 0, 0) }, MetAt(new IsRangeEndCondition(additionalCandles: Candles)));
        CollectionAssert.AreEqual(new List<DateTime> { new(2021, 11, 26, 8, 0, 0) }, MetAt(new IsRangeEndCondition(EnumSwingDirection.Down, Candles)));
        Assert.AreEqual(0, MetAt(new IsRangeEndCondition(EnumSwingDirection.Up, Candles)).Count);
    }

    [TestMethod]
    public void BuyLowZone_ByClose()
    {
        // The buy-low zone runs from the lower band, 55,160, to 25% up the range, 56,885.
        var met = MetAt(new IsInRangeZoneCondition(EnumRangeZone.BuyLow, EnumPriceBasis.Close, Candles));

        Assert.IsFalse(met.Contains(new DateTime(2021, 11, 22, 0, 0, 0)), "the identifying close, 57,431, is above the zone");
        Assert.IsTrue(met.Contains(new DateTime(2021, 11, 22, 16, 0, 0)), "56,021 closes in the zone");
        Assert.IsTrue(met.Contains(new DateTime(2021, 11, 23, 4, 0, 0)), "56,031 closes in the zone");
        Assert.IsFalse(met.Contains(new DateTime(2021, 11, 23, 16, 0, 0)), "57,793 closes above the zone");
        Assert.IsFalse(met.Contains(new DateTime(2021, 11, 26, 8, 0, 0)), "the range has ended on the close below the band");
    }

    [TestMethod]
    public void BuyLowZone_ByWick_ReachesIntoTheZoneEarlier()
    {
        var byClose = MetAt(new IsInRangeZoneCondition(EnumRangeZone.BuyLow, EnumPriceBasis.Close, Candles));
        var byWick = MetAt(new IsInRangeZoneCondition(EnumRangeZone.BuyLow, EnumPriceBasis.Wick, Candles));

        Assert.IsTrue(byWick.Contains(new DateTime(2021, 11, 22, 4, 0, 0)), "the low of 56,827 reaches into the zone though the close, 57,314, does not");
        Assert.IsFalse(byClose.Contains(new DateTime(2021, 11, 22, 4, 0, 0)));
        Assert.IsTrue(byClose.All(byWick.Contains), "a close in the zone is always reached by the wick");
    }

    [TestMethod]
    public void SellHighZone_ByCloseAndByWick()
    {
        // The sell-high zone runs from 75% up the range, 58,802, to the upper band, 60,527.
        var byClose = MetAt(new IsInRangeZoneCondition(EnumRangeZone.SellHigh, EnumPriceBasis.Close, Candles));
        var byWick = MetAt(new IsInRangeZoneCondition(EnumRangeZone.SellHigh, EnumPriceBasis.Wick, Candles));

        Assert.IsTrue(byClose.Count > 0, "the bounce of the 25th closes in the sell-high zone");
        Assert.IsTrue(byClose.All(x => x >= new DateTime(2021, 11, 24, 0, 0, 0)), "nothing closes that high before the 24th");
        Assert.IsTrue(byClose.All(byWick.Contains));
        Assert.IsTrue(byWick.Contains(new DateTime(2021, 11, 22, 12, 0, 0)), "the high of 59,526 on the 22nd reaches the zone though the close does not");
    }
}
