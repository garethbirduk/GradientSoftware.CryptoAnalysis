using Gradient.CryptoAnalysis.Strategies;

namespace Gradient.CryptoAnalysis.Test.Strategies;

[TestClass]
public class StrategyBacktestTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<Price> Candles(params (double Open, double High, double Low, double Close)[] candles) =>
        candles.Select((c, i) => new Price { DateTime = Start.AddHours(i), Open = c.Open, High = c.High, Low = c.Low, Close = c.Close }).ToList();

    // Four green candles with bodies of 10, 20, 10 and 20 (an average of 15), closing at 160.
    private static readonly (double, double, double, double)[] FourGreen = [(100, 112, 99, 110), (110, 131, 109, 130), (130, 141, 129, 140), (140, 161, 139, 160)];

    private static Strategy RunOf(int length = 4, EnumTradeDirection direction = EnumTradeDirection.Long, EnumCandleColour colour = EnumCandleColour.Green) => new()
    {
        Id = "test",
        Direction = direction,
        Entry = new StrategyEntry { Colour = colour, Length = length },
        TakeProfit = new StrategyTarget { Percentage = 100, Candles = length },
        StopLoss = new StrategyTarget { Percentage = 100, Candles = length },
    };

    [TestMethod]
    public void Run_EntersAtTheCloseOfTheRunWithTargetsFromTheAverageBody()
    {
        var prices = Candles([.. FourGreen, (160, 176, 158, 170)]);

        var trade = StrategyBacktest.Run(prices, RunOf()).Trades.Single();

        Assert.AreEqual((3, 160.0, 175.0, 145.0), (trade.EntryIndex, trade.EntryPrice, trade.TakeProfit, trade.StopLoss));
        Assert.AreEqual((4, 175.0, EnumTradeOutcome.TakeProfit, 15.0), (trade.ExitIndex, trade.ExitPrice, trade.Outcome, trade.Profit));
    }

    [TestMethod]
    public void Run_StopLossIsHitOnTheLow()
    {
        var prices = Candles([.. FourGreen, (160, 165, 150, 152), (152, 153, 140, 141)]);

        var trade = StrategyBacktest.Run(prices, RunOf()).Trades.Single();

        Assert.AreEqual((5, 145.0, EnumTradeOutcome.StopLoss, -15.0), (trade.ExitIndex, trade.ExitPrice, trade.Outcome, trade.Profit));
    }

    [TestMethod]
    public void Run_CandleReachingBothTargetsCountsAsTheStopLoss()
    {
        var prices = Candles([.. FourGreen, (160, 180, 140, 160)]);

        var trade = StrategyBacktest.Run(prices, RunOf()).Trades.Single();

        Assert.AreEqual((EnumTradeOutcome.StopLoss, 145.0, true), (trade.Outcome, trade.ExitPrice, trade.BothTouched));
    }

    [TestMethod]
    public void Run_GapPastTheStopLossFillsAtTheOpen()
    {
        var prices = Candles([.. FourGreen, (140, 150, 130, 135)]);

        var trade = StrategyBacktest.Run(prices, RunOf()).Trades.Single();

        Assert.AreEqual((EnumTradeOutcome.StopLoss, 140.0, -20.0), (trade.Outcome, trade.ExitPrice, trade.Profit));
    }

    [TestMethod]
    public void Run_LongerRunEntersOnce()
    {
        var prices = Candles([.. FourGreen, (160, 166, 159, 165), (165, 171, 164, 170)]);

        var trades = StrategyBacktest.Run(prices, RunOf()).Trades;

        Assert.AreEqual(1, trades.Count);
        Assert.AreEqual(3, trades[0].EntryIndex);
    }

    [TestMethod]
    public void Run_TradeStillRunningAtTheEndIsOpenAndNotCounted()
    {
        var prices = Candles([.. FourGreen, (160, 166, 159, 165)]);

        var run = StrategyBacktest.Run(prices, RunOf());

        Assert.AreEqual((EnumTradeOutcome.Open, 165.0, 5.0), (run.Trades[0].Outcome, run.Trades[0].ExitPrice, run.Trades[0].Profit));
        Assert.AreEqual((0, 1, 0.0), (run.Summary.Trades, run.Summary.StillOpen, run.Summary.TotalProfit));
    }

    [TestMethod]
    public void Run_ShortAfterRedRunTakesProfitBelow()
    {
        // Four red candles with bodies of 10, closing at 60: take profit at 50, stop loss at 70.
        var prices = Candles((100, 101, 89, 90), (90, 91, 79, 80), (80, 81, 69, 70), (70, 71, 59, 60), (60, 62, 48, 49));

        var trade = StrategyBacktest.Run(prices, RunOf(direction: EnumTradeDirection.Short, colour: EnumCandleColour.Red)).Trades.Single();

        Assert.AreEqual((50.0, 70.0), (trade.TakeProfit, trade.StopLoss));
        Assert.AreEqual((EnumTradeOutcome.TakeProfit, 10.0), (trade.Outcome, trade.Profit));
    }

    [TestMethod]
    public void Run_OnePositionAtATimeSkipsRunsWhileATradeIsOpen()
    {
        // Two runs of three: the first trade is still open when the second run ends.
        var prices = Candles((100, 111, 99, 110), (110, 121, 109, 120), (120, 131, 119, 130), (130, 131, 125, 128),
            (128, 130, 127, 129), (129, 131, 128, 130), (130, 132, 129, 131), (131, 160, 130, 150));
        var strategy = RunOf(length: 3);

        Assert.AreEqual(1, StrategyBacktest.Run(prices, strategy).Trades.Count);
        strategy.OnePositionAtATime = false;
        Assert.AreEqual(2, StrategyBacktest.Run(prices, strategy).Trades.Count);
    }

    // A red candle with a body of 10 opening at o, and a green one.
    private static (double, double, double, double) Red(double o) => (o, o + 1, o - 11, o - 10);

    private static (double, double, double, double) Green(double o) => (o, o + 11, o - 1, o + 10);

    // Two red candles, then three green: a chain of the two, the green within the given number of candles.
    private static Strategy RedThenGreen(int? within = null, int red = 2, EnumCandleColour first = EnumCandleColour.Red) => new()
    {
        Id = "chain",
        After = [new StrategyEntry { Colour = first, Length = red }],
        Entry = new StrategyEntry { Colour = EnumCandleColour.Green, Length = 3, Within = within },
        TakeProfit = new StrategyTarget { Type = StrategyTarget.Percent, Percentage = 50 },
        StopLoss = new StrategyTarget { Type = StrategyTarget.Percent, Percentage = 50 },
        OnePositionAtATime = false,
    };

    [TestMethod]
    public void Run_Chain_EntersOnlyOnceTheStepBeforeIsMet()
    {
        // Three green with nothing before them, then two red and three green.
        var prices = Candles(Green(100), Green(110), Green(120), Red(130), Red(120), Green(110), Green(120), Green(130));

        var trade = StrategyBacktest.Run(prices, RedThenGreen()).Trades.Single();

        Assert.AreEqual(7, trade.EntryIndex);
        CollectionAssert.AreEqual(new[] { 4 }, trade.Met.Select(x => x.Index).ToArray());
        Assert.AreEqual(3, trade.FirstRead(RedThenGreen()), "The chain reads from the first of the red.");
    }

    [TestMethod]
    [DataRow(3, 1)]
    [DataRow(2, 0)]
    public void Run_Chain_TheNextStepMustBeMetWithinItsCandles(int within, int trades)
    {
        // The second red at #1, the third green at #4: three candles on.
        var prices = Candles(Red(100), Red(90), Green(80), Green(90), Green(100));

        Assert.AreEqual(trades, StrategyBacktest.Run(prices, RedThenGreen(within)).Trades.Count);
    }

    [TestMethod]
    public void Run_Chain_StartsAgainAfterItIsMet()
    {
        // Two red and three green, a single red, then three green again: the second run has no two red before it of its own.
        var once = Candles(Red(100), Red(90), Green(80), Green(90), Green(100), Red(110), Green(100), Green(110), Green(120));
        var twice = Candles(Red(100), Red(90), Green(80), Green(90), Green(100), Red(110), Red(100), Green(90), Green(100), Green(110));

        Assert.AreEqual(1, StrategyBacktest.Run(once, RedThenGreen()).Trades.Count);
        Assert.AreEqual(2, StrategyBacktest.Run(twice, RedThenGreen()).Trades.Count);
    }

    [TestMethod]
    public void Run_Chain_AStepReadsOnlyCandlesAfterTheStepBefore()
    {
        // Six green in a row: two green are met at #1, but the run of three that meets the entry begins at #0, before it.
        var prices = Candles(Green(100), Green(110), Green(120), Green(130), Green(140), Green(150));

        Assert.AreEqual(0, StrategyBacktest.Run(prices, RedThenGreen(first: EnumCandleColour.Green)).Trades.Count);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(12)]
    public void Occurrences_OfAChain_AreTheEntriesWhenTradesMayOverlap(int? within)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "COINBASE_BTCUSD, 60.csv");
        var prices = new Csv.CsvReaderHelper().ReadData<Price, global::CryptoAnalysis.Csv.ClassMaps.PriceClassMap>(path).ToList();
        var strategy = RedThenGreen(within, red: 3);

        var entries = StrategyBacktest.Run(prices, strategy).Trades.Select(t => t.EntryIndex).ToList();

        CollectionAssert.AreEqual(StrategyBacktest.Occurrences(prices, strategy), entries);
        Assert.IsTrue(entries.Count > 10);
    }

    [TestMethod]
    public void Parse_ReadsAChain()
    {
        var book = StrategyBook.Parse("""
            { "strategies": [ { "id": "chain", "name": "Chain", "direction": "Long",
              "after": [ { "type": "SuccessiveCandles", "colour": "Red", "length": 3 } ],
              "entry": { "type": "SuccessiveCandles", "colour": "Green", "length": 4, "within": 10 },
              "takeProfit": { "type": "Percent", "percent": 2 }, "stopLoss": { "type": "Percent", "percent": 2 } } ] }
            """);

        var s = book.Strategies.Single();
        Assert.AreEqual((2, EnumCandleColour.Red, 10), (s.Steps.Count, s.Steps[0].Colour, s.Entry.Within));
        Assert.AreEqual(0, s.Validate().Count);
        s.After[0].Within = 5;
        CollectionAssert.Contains(s.Validate(), "Step 1: Within needs a step before it.");
    }

    [TestMethod]
    public void Run_WickTargets_AreTheHighestHighAndLowestLowOfTheLastCandles()
    {
        // The four green have highs up to 161 and lows down to 99; the next candle reaches 161.
        var prices = Candles([.. FourGreen, (160, 176, 158, 170)]);
        var strategy = RunOf();
        strategy.TakeProfit = new StrategyTarget { Type = StrategyTarget.HighestWick, Candles = 4 };
        strategy.StopLoss = new StrategyTarget { Type = StrategyTarget.LowestWick, Candles = 4 };

        var trade = StrategyBacktest.Run(prices, strategy).Trades.Single();

        Assert.AreEqual((161.0, 99.0), (trade.TakeProfit, trade.StopLoss));
        Assert.AreEqual((EnumTradeOutcome.TakeProfit, 1.0), (trade.Outcome, trade.Profit));
    }

    [TestMethod]
    public void Run_WickTargetNotBeyondTheEntry_EntersNoTrade()
    {
        // A Long's Take Profit at the lowest low is below the entry, so no trade can be set there.
        var prices = Candles([.. FourGreen, (160, 176, 158, 170)]);
        var strategy = RunOf();
        strategy.TakeProfit = new StrategyTarget { Type = StrategyTarget.LowestWick, Candles = 4 };

        Assert.AreEqual(0, StrategyBacktest.Run(prices, strategy).Trades.Count);
        Assert.IsFalse(StrategyBacktest.Measurable(prices, 3, strategy));
    }

    [TestMethod]
    public void Run_StopLossForATargetInR_IsTheTakeProfitsDistanceDividedByIt()
    {
        // A Take Profit 15 above the entry at 160; for a 2R target the Stop Loss is 7.5 below it, and the next low of 150 reaches it.
        var prices = Candles([.. FourGreen, (160, 165, 150, 152)]);
        var strategy = RunOf();
        strategy.StopLoss = new StrategyTarget { Type = StrategyTarget.RiskRatio, Ratio = 2 };

        var trade = StrategyBacktest.Run(prices, strategy).Trades.Single();

        Assert.AreEqual((175.0, 152.5), (trade.TakeProfit, trade.StopLoss));
        Assert.AreEqual((EnumTradeOutcome.StopLoss, -7.5), (trade.Outcome, trade.Profit));
    }

    [TestMethod]
    public void Validate_TakeProfitAtARatioOfTheReward_IsRefused()
    {
        var strategy = RunOf();
        strategy.TakeProfit = new StrategyTarget { Type = StrategyTarget.RiskRatio };

        CollectionAssert.Contains(strategy.Validate(), "Take Profit: a target in R sets the Stop Loss from the Take Profit, so only the Stop Loss can be one.");
    }

    [TestMethod]
    public void Summary_TotalsTheClosedTrades()
    {
        // A win of 15, a red candle, then four green candles entering at 226 and a loss of 15 at 211.
        var prices = Candles([.. FourGreen, (160, 176, 158, 170), (170, 171, 165, 166),
            (166, 177, 165, 176), (176, 197, 175, 196), (196, 207, 195, 206), (206, 227, 205, 226), (226, 228, 205, 210)]);

        var summary = StrategyBacktest.Run(prices, RunOf()).Summary;

        Assert.AreEqual((2, 1, 1, 50.0, 0.0, 1.0), (summary.Trades, summary.Won, summary.Lost, summary.WinRate, summary.TotalProfit, summary.ProfitFactor));
    }

    [TestMethod]
    [DataRow(EnumCandleColour.Green, 4)]
    [DataRow(EnumCandleColour.Red, 4)]
    [DataRow(EnumCandleColour.Green, 7)]
    public void Occurrences_AreTheEntriesWhenTradesMayOverlap(EnumCandleColour colour, int length)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "COINBASE_BTCUSD, 60.csv");
        var prices = new Csv.CsvReaderHelper().ReadData<Price, global::CryptoAnalysis.Csv.ClassMaps.PriceClassMap>(path).ToList();
        var strategy = RunOf(length, colour: colour);
        strategy.OnePositionAtATime = false;

        var entries = StrategyBacktest.Run(prices, strategy).Trades.Select(t => t.EntryIndex).ToList();

        CollectionAssert.AreEqual(StrategyBacktest.Occurrences(prices, strategy), entries);
        Assert.IsTrue(entries.Count > 10);
    }

    [TestMethod]
    public void Parse_ReadsTheEditorsFormat()
    {
        var book = StrategyBook.Parse("""
            { "strategies": [ { "id": "run-4", "name": "Run of 4", "direction": "Long",
              "entry": { "type": "SuccessiveCandles", "colour": "Green", "length": 4 },
              "takeProfit": { "type": "AverageCandle", "percent": 80, "candles": 4, "measure": "Range" },
              "stopLoss": { "type": "Percent", "percent": 2 } } ] }
            """);

        var s = book.Strategies.Single();
        Assert.AreEqual((4, 80.0, EnumCandleMeasure.Range, StrategyTarget.Percent, 2.0), (s.Entry.Length, s.TakeProfit.Percentage, s.TakeProfit.Measure, s.StopLoss.Type, s.StopLoss.Percentage));
        Assert.AreEqual(0, s.Validate().Count);
    }
}
