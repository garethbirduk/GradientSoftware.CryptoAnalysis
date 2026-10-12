using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Conditions.PriceConditions;
using Gradient.CryptoAnalysis.Csv;
using Gradient.CryptoAnalysis.Strategies;

namespace Gradient.CryptoAnalysis.Test.Strategies;

/// <summary>
/// The Range trade as a strategy: a step met in a Range's zone, kills that end a wait, and exits on the Range's lines. On
/// the training's own example (4-hour candles, November 2021; Range 55,927 to 59,761 from 22 Nov 00:00, buy-low zone up to
/// 56,885, sell-high zone from 58,802, lower band 55,160).
/// </summary>
[TestClass]
public class StrategyRangeTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<Price> Training() =>
        new CsvReaderHelper().ReadData<Price, PriceClassMap>(Path.Combine("TestData", "Terms", "Range", "04-the-training-example.csv")).ToList();

    private static List<Price> Candles(params (double Open, double Close)[] candles) =>
        candles.Select((c, i) => new Price { DateTime = Start.AddHours(i), Open = c.Open, High = Math.Max(c.Open, c.Close), Low = Math.Min(c.Open, c.Close), Close = c.Close }).ToList();

    private static StrategyEntry Zone(bool killed = true, int level = 1) =>
        new() { Type = StrategyEntry.RangeZone, Level = level, Kill = killed ? [new StrategyEntry { Type = StrategyEntry.RangeEnd }] : null };

    // The Range trade: steps, then the entry, with the exits on the Range's lines.
    private static Strategy RangeTrade(StrategyEntry entry, params StrategyEntry[] after) => new()
    {
        Id = "range",
        After = [.. after],
        Entry = entry,
        TakeProfit = new StrategyTarget { Type = StrategyTarget.RangeZone },
        StopLoss = new StrategyTarget { Type = StrategyTarget.RangeBand },
    };

    private static StrategyEntry Green(int length) => new() { Colour = EnumCandleColour.Green, Length = length };

    [TestMethod]
    public void Run_RangeZone_EntersAtTheCloseOfACandleThatReachesIntoTheBuyLowZone()
    {
        // The three trades drawn on the training's chart, entered at the close: two reach the sell-high zone, and the third
        // is stopped by the candle that ends the Range.
        var prices = Training();

        var trades = StrategyBacktest.Run(prices, RangeTrade(Zone(killed: false))).Trades;

        CollectionAssert.AreEqual(new[] { 69, 71, 93 }, trades.Select(x => x.EntryIndex).ToArray());
        CollectionAssert.AreEqual(new[] { EnumTradeOutcome.TakeProfit, EnumTradeOutcome.TakeProfit, EnumTradeOutcome.StopLoss }, trades.Select(x => x.Outcome).ToArray());
        foreach (var trade in trades)
        {
            Assert.AreEqual(58802.8, trade.TakeProfit, 1, "Take Profit at the bottom of the sell-high zone.");
            Assert.AreEqual(55160.4, trade.StopLoss, 1, "Stop Loss at the lower band.");
            Assert.AreEqual((1, 68, 40), (trade.Range!.Level, trade.Range.Identified, trade.EntryFrom), "The Range held, and where its Retracement is measured from.");
        }

        Assert.AreEqual((trades[0].TakeProfit - prices[69].Close) / (prices[69].Close - trades[0].StopLoss), trades[0].R, 0.0001, "The target in R, from the entry at the close.");
    }

    [TestMethod]
    public void Run_RangeZone_TheSellHighZoneIsReachedByAHigh()
    {
        var prices = Training();
        var strategy = RangeTrade(new StrategyEntry { Type = StrategyEntry.RangeZone, Level = 1, Zone = EnumRangeZone.SellHigh });
        strategy.Direction = EnumTradeDirection.Short;

        var trade = StrategyBacktest.Run(prices, strategy).Trades[0];

        Assert.IsTrue(prices[trade.EntryIndex].High >= 58802.8, $"#{trade.EntryIndex}");
        Assert.AreEqual(56885.4, trade.TakeProfit, 1, "A Short's Take Profit at the top of the buy-low zone.");
        Assert.AreEqual(60527.9, trade.StopLoss, 1, "Its Stop Loss at the upper band.");
    }

    [TestMethod]
    public void Signals_RangeSweep_IsAWickBelowTheRangesLowThatClosesBackInside()
    {
        // The Range's low is 55,927. #71 reaches into the buy-low zone but stays above the low; #72 wicks to 55,826 and
        // closes at 56,022.
        var prices = Training();
        var strategy = RangeTrade(new StrategyEntry { Type = StrategyEntry.RangeSweep, Level = 1 });
        strategy.OnePositionAtATime = false;

        var swept = StrategyBacktest.Signals(prices, strategy).Select(x => x.Index).ToList();

        CollectionAssert.AreEqual(new[] { 72, 73, 75, 76, 83 }, swept);
        foreach (var i in swept)
            Assert.IsTrue(prices[i].Low < 55927.9 && prices[i].Close >= 55927.9, $"#{i}");
        CollectionAssert.AreEqual(swept, StrategyBacktest.Occurrences(prices, strategy));

        var trade = StrategyBacktest.Run(prices, strategy).Trades[0];
        Assert.AreEqual(72, trade.EntryIndex);
        Assert.AreEqual(58802.8, trade.TakeProfit, 1, "The exits are on the Range's lines, as a zone step's are.");
        Assert.AreEqual(55160.4, trade.StopLoss, 1);
    }

    [TestMethod]
    public void Run_Chain_WaitsForTwoGreenCandlesAfterTheZone()
    {
        var prices = Training();

        var trade = StrategyBacktest.Run(prices, RangeTrade(Green(2), Zone())).Trades.Single();

        // #72 reaches into the zone; #73 and #74 are the two green candles after it.
        Assert.AreEqual((74, 72, 40), (trade.EntryIndex, trade.Met[0].Index, trade.FirstRead));
        Assert.AreEqual(58802.8, trade.TakeProfit, 1, "The exits stay on the Range's lines, however late the entry.");
        Assert.AreEqual(55160.4, trade.StopLoss, 1);
        Assert.AreEqual(EnumTradeOutcome.TakeProfit, trade.Outcome);
    }

    [TestMethod]
    public void Signals_ARangesEnd_EndsTheWait()
    {
        // #93 reaches into the zone and #94 closes below the band, ending the Range. Two green candles come after.
        var prices = Training();

        var killed = StrategyBacktest.Signals(prices, RangeTrade(Green(2), Zone())).Select(x => x.Index).ToList();
        var kept = StrategyBacktest.Signals(prices, RangeTrade(Green(2), Zone(killed: false))).Select(x => x.Index).ToList();

        Assert.IsTrue(killed.All(x => x <= 93), string.Join(" ", killed));
        Assert.IsTrue(kept.Any(x => x > 94), string.Join(" ", kept));
        CollectionAssert.AreEqual(killed, kept.Where(x => x <= 93).ToList(), "The kill changes nothing while the Range holds.");
    }

    [TestMethod]
    public void Signals_AKill_EndsTheWaitOfItsStepAndThoseAfterIt()
    {
        // Three green candles, a red, then two green: the red candle is a kill of the first step.
        var prices = Candles((10, 11), (11, 12), (12, 13), (13, 12.5), (12.5, 13), (13, 14));
        var strategy = new Strategy
        {
            Id = "kill",
            After = [new StrategyEntry { Colour = EnumCandleColour.Green, Length = 3, Kill = [new StrategyEntry { Colour = EnumCandleColour.Red, Length = 1 }] }],
            Entry = Green(2),
            TakeProfit = new StrategyTarget { Type = StrategyTarget.Percent, Percentage = 50 },
            StopLoss = new StrategyTarget { Type = StrategyTarget.Percent, Percentage = 50 },
        };

        Assert.AreEqual(0, StrategyBacktest.Signals(prices, strategy).Count, "The red candle at #3 ends the wait the three green began.");
        CollectionAssert.AreEqual(new List<int>(), StrategyBacktest.Occurrences(prices, strategy));

        strategy.After[0].Kill = null;
        CollectionAssert.AreEqual(new[] { 5 }, StrategyBacktest.Signals(prices, strategy).Select(x => x.Index).ToArray());
        CollectionAssert.AreEqual(new List<int> { 5 }, StrategyBacktest.Occurrences(prices, strategy));
    }

    [TestMethod]
    [DataRow(1, true)]
    [DataRow(2, true)]
    [DataRow(3, true)]
    [DataRow(1, false)]
    public void Occurrences_OfTheRangeTrade_AreTheEntriesWhenTradesMayOverlap(int level, bool killed)
    {
        var prices = Btc.From2023().Take(6000).ToList();
        var strategy = RangeTrade(Green(2), Zone(killed, level));
        strategy.OnePositionAtATime = false;

        var signals = StrategyBacktest.Signals(prices, strategy).Select(x => x.Index).ToList();

        CollectionAssert.AreEqual(StrategyBacktest.Occurrences(prices, strategy), signals);
        Assert.IsTrue(signals.Count > 0, $"{signals.Count} signals");
    }

    [TestMethod]
    public void Validate_SaysWhereAKillOrARangesLineCannotBe()
    {
        var strategy = RangeTrade(Green(2), Zone());
        Assert.AreEqual(0, strategy.Validate().Count, string.Join(" ", strategy.Validate()));

        strategy.Entry.Kill = [Green(1)];
        strategy.After = [new StrategyEntry { Colour = EnumCandleColour.Red, Length = 2, Kill = [new StrategyEntry { Type = StrategyEntry.RangeEnd }, new StrategyEntry { Type = "Nothing" }] }];

        CollectionAssert.AreEqual(new[]
        {
            "Step 1: A Range's end is a kill of the Range step whose Range it ends.",
            "Step 1: Kill 2: \"Nothing\" is not a kill type: SuccessiveCandles, BreakOfStructure, MarketStructureBreak, Swing, Trend, RangeZone, RangeSweep, RangeEnd.",
            "Entry: a kill ends the wait that follows a step, and nothing is waited for after the entry.",
            "A target on a Range's lines needs a Range step in the entry condition.",
        }, strategy.Validate());
    }

    [TestMethod]
    public void Parse_ReadsTheRangeTrade()
    {
        var book = StrategyBook.Parse("""
            { "strategies": [ { "id": "range-trade", "name": "Range trade", "direction": "Long",
              "after": [ { "type": "RangeZone", "zone": "BuyLow", "level": 1, "depth": 30, "retracement": 80, "band": 15, "kill": [ { "type": "RangeEnd" } ] },
                         { "type": "Trend", "direction": "Up", "level": 2, "swings": 2 } ],
              "entry": { "type": "SuccessiveCandles", "colour": "Green", "length": 2 },
              "takeProfit": { "type": "RangeZone" }, "stopLoss": { "type": "RangeBand" } } ] }
            """);

        var s = book.Strategies.Single();
        var zone = s.After[0];
        Assert.AreEqual((StrategyEntry.RangeZone, EnumRangeZone.BuyLow, 1, 30.0, 80.0, 15.0), (zone.Type, zone.Zone, zone.Level, zone.Depth, zone.Retracement, zone.Band));
        Assert.AreEqual(StrategyEntry.RangeEnd, zone.Kill!.Single().Type);
        Assert.IsTrue(s.TakeProfit.IsRange && s.StopLoss.IsRange);
        Assert.AreEqual(0, s.Validate().Count, string.Join(" ", s.Validate()));
    }
}
