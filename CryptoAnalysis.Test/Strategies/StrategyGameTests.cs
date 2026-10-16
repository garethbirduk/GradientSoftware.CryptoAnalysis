using Gradient.CryptoAnalysis.Strategies;

namespace Gradient.CryptoAnalysis.Test.Strategies;

[TestClass]
public class StrategyGameTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<Price> Candles(params (double Open, double High, double Low, double Close)[] candles) =>
        candles.Select((c, i) => new Price { DateTime = Start.AddHours(i), Open = c.Open, High = c.High, Low = c.Low, Close = c.Close }).ToList();

    // Three green candles closing at 130, then one that reaches 1% above the close, then one that falls 1% below.
    private static readonly (double, double, double, double)[] Greens = [(100, 111, 99, 110), (110, 121, 109, 120), (120, 131, 119, 130)];

    private static Strategy Greens3(string id = "a", double percent = 1) => new()
    {
        Id = id,
        Entry = new StrategyEntry { Colour = EnumCandleColour.Green, Length = 3 },
        TakeProfit = new StrategyTarget { Type = StrategyTarget.Percent, Percentage = percent },
        StopLoss = new StrategyTarget { Type = StrategyTarget.Percent, Percentage = percent },
        CountFees = false,
        CountSlippage = false,
    };

    [TestMethod]
    public void TradesIn_MoneyIsTheStakesShareOfTheProfitPercent()
    {
        var prices = Candles([.. Greens, (130, 132, 129, 131)]);
        var strategy = Greens3();

        var trade = StrategyGame.TradesIn(prices, strategy, StrategyBacktest.Signals(prices, strategy), 0, prices.Count, 1000).Single();

        Assert.AreEqual((2, EnumTradeOutcome.TakeProfit, false), (trade.Trade.EntryIndex, trade.Trade.Outcome, trade.Refunded));
        Assert.AreEqual(10, trade.Money, 1e-9);
    }

    [TestMethod]
    public void TradesIn_ATradeClosingAfterTheWindowIsRefunded()
    {
        var prices = Candles([.. Greens, (130, 132, 129, 131)]);
        var strategy = Greens3();

        // The window ends before the candle that reaches the Take Profit.
        var trade = StrategyGame.TradesIn(prices, strategy, StrategyBacktest.Signals(prices, strategy), 0, 3, 1000).Single();

        Assert.AreEqual((true, 0.0), (trade.Refunded, trade.Money));
    }

    [TestMethod]
    public void TradesIn_EntersOnlyInTheWindow()
    {
        var prices = Candles([.. Greens, (130, 132, 129, 131)]);
        var strategy = Greens3();

        var trades = StrategyGame.TradesIn(prices, strategy, StrategyBacktest.Signals(prices, strategy), 3, prices.Count, 1000);

        Assert.AreEqual(0, trades.Count);
    }

    [TestMethod]
    public void Round_PicksAWindowEveryContenderTradesInAndNamesTheWinner()
    {
        // Twenty flat candles, then three greens and a candle up 1%, which a tighter strategy wins; the price then holds, so a
        // wider one is still open at the window's end and refunded.
        var flat = Enumerable.Repeat((100.0, 100.5, 99.5, 100.0), 20).ToArray();
        var held = Enumerable.Repeat((131.0, 131.5, 130.5, 131.0), 20).ToArray();
        var prices = Candles([.. flat, .. Greens, (130, 132, 129, 131), .. held]);
        var a = Greens3("a", 1);
        var b = Greens3("b", 5);

        var round = StrategyGame.Round(prices, "test", [a, b], 8, 1000, new Random(1));

        Assert.AreEqual(2, round.Contenders.Count);
        Assert.IsTrue(round.Contenders.All(c => c.Trades.Count == 1), "every contender trades in the window");
        Assert.AreEqual(8, round.End - round.First);
        Assert.AreEqual(0, round.Winner);
        Assert.AreEqual(10, round.Contenders[0].Total, 1e-9);
        Assert.AreEqual((0.0, 1), (round.Contenders[1].Total, round.Contenders[1].Refunded));
        Assert.AreEqual((prices[round.First].DateTime, prices[round.End - 1].DateTime), (round.From, round.To));
    }

    [TestMethod]
    public void Round_IsTheSameAgainFromTheSameSeed()
    {
        var flat = Enumerable.Repeat((100.0, 100.5, 99.5, 100.0), 30).ToArray();
        var prices = Candles([.. flat, .. Greens, (130, 132, 129, 131), .. flat, .. Greens, (130, 131, 128, 129), .. flat]);

        var one = StrategyGame.Round(prices, "test", [Greens3()], 6, 1000, new Random(7));
        var again = StrategyGame.Round(prices, "test", [Greens3()], 6, 1000, new Random(7));

        Assert.AreEqual(one.First, again.First);
    }
}
