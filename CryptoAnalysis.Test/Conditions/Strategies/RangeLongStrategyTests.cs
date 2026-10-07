using CryptoAnalysis.Conditions;
using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Conditions.Strategies;
using Gradient.CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis.Test.Conditions.Strategies;

/// <summary>
/// The range trade on the training's own example (4-hour candles, November 2021; range 55,927 to 59,761 from 22 Nov 00:00,
/// buy-low zone up to 56,885, sell-high zone from 58,802, lower band 55,160): the trades it gives and what each one did.
/// </summary>
[TestClass]
public class RangeLongStrategyTests
{
    private static readonly string DataPath = Path.Combine("TestData", "Terms", "Range", "04-the-training-example.csv");
    private List<Price> _prices = [];

    [TestInitialize]
    public void TestInitialize()
    {
        _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(DataPath).ToList();
    }

    private List<Trade> Completed(RangeLongStrategy strategy, out Backtest backtest)
    {
        backtest = new Backtest { PositionRules = strategy.Rules(), Prices = _prices, StartDateTime = _prices[0].DateTime };
        backtest.Execute();
        return backtest.Trades.Where(x => x.TradeStatus == EnumConditionStatus.Completed).OrderBy(x => x.DateTimeOpen).ToList();
    }

    [TestMethod]
    public void EntriesByWick_TwoWinsThenTheStop()
    {
        // The three trades drawn on the chart: into the zone on the wick, out at the sell-high zone twice, then stopped
        // by the candle that ended the range.
        var trades = Completed(new RangeLongStrategy(EnumPriceBasis.Wick), out var backtest);

        Assert.AreEqual(3, trades.Count, string.Join("\n", backtest.Trades.Select(Describe)));
        foreach (var trade in trades)
        {
            Assert.AreEqual(58802.8, trade.TakeProfitTarget, 1, "take profit at the bottom of the sell-high zone");
            Assert.AreEqual(55160.4, trade.StopLossTarget, 1, "stop loss at the lower band");
        }

        // 22 Nov 04:00 dips to 56,827: the limit at the zone's top, 56,885, fills there; 12:00 reaches 59,526, the target.
        Assert.AreEqual(new DateTime(2021, 11, 22, 4, 0, 0), trades[0].DateTimeOpen, Describe(trades[0]));
        Assert.AreEqual(56885.4, trades[0].PriceOpen, 1, Describe(trades[0]));
        Assert.AreEqual(new DateTime(2021, 11, 22, 12, 0, 0), trades[0].DateTimeClose, Describe(trades[0]));
        Assert.AreEqual(58802.8, trades[0].PriceClose, 1, Describe(trades[0]));
        // 12:00 also dips to 56,641 on its way, so the next limit fills on the same candle; the bounce of the 25th reaches
        // the target at 12:00.
        Assert.AreEqual(new DateTime(2021, 11, 22, 12, 0, 0), trades[1].DateTimeOpen, Describe(trades[1]));
        Assert.AreEqual(56885.4, trades[1].PriceOpen, 1, Describe(trades[1]));
        Assert.AreEqual(new DateTime(2021, 11, 25, 12, 0, 0), trades[1].DateTimeClose, Describe(trades[1]));
        Assert.IsTrue(trades[1].PriceClose > trades[1].PriceOpen);
        // 26 Nov 04:00 dips to 56,656 and fills; 08:00 falls through the band, which stops the trade and ends the range.
        Assert.AreEqual(new DateTime(2021, 11, 26, 4, 0, 0), trades[2].DateTimeOpen, Describe(trades[2]));
        Assert.AreEqual(new DateTime(2021, 11, 26, 8, 0, 0), trades[2].DateTimeClose, Describe(trades[2]));
        Assert.AreEqual(55160.4, trades[2].PriceClose, 1, Describe(trades[2]));
        Assert.AreEqual(0, backtest.Trades.Count(x => x.TradeStatus is EnumConditionStatus.Open or EnumConditionStatus.AwaitingConfirmation or EnumConditionStatus.Confirmed),
            "nothing is left open or waiting once the range has ended");
    }

    [TestMethod]
    public void EntriesByClose_OneTradeOnly()
    {
        // By close, 22 Nov 16:00 is the first close in the zone and fills at that close, 56,021; nothing closes in the zone
        // again after the bounce, so the first dip's quick trade and the last dip's stop are both missed.
        var trades = Completed(new RangeLongStrategy(EnumPriceBasis.Close), out var backtest);

        Assert.AreEqual(1, trades.Count, string.Join("\n", backtest.Trades.Select(Describe)));
        Assert.AreEqual(new DateTime(2021, 11, 22, 16, 0, 0), trades[0].DateTimeOpen, Describe(trades[0]));
        Assert.AreEqual(56021.68, trades[0].PriceOpen, 1, Describe(trades[0]));
        Assert.AreEqual(new DateTime(2021, 11, 25, 12, 0, 0), trades[0].DateTimeClose, Describe(trades[0]));
        Assert.IsTrue(trades[0].PriceClose > trades[0].PriceOpen);
    }

    private static string Describe(Trade t) =>
        $"{t.TradeStatus}: open {t.DateTimeOpen:yyyy-MM-dd HH:mm} at {t.PriceOpen:F0}, close {t.DateTimeClose:yyyy-MM-dd HH:mm} at {t.PriceClose:F0} (tp {t.TakeProfitTarget:F0}, sl {t.StopLossTarget:F0})";
}
