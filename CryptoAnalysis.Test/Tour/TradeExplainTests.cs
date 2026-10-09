using System.Text.Json.Nodes;
using Gradient.CryptoAnalysis.Site;
using Gradient.CryptoAnalysis.Strategies;

namespace Gradient.CryptoAnalysis.Test.Tour;

/// <summary>
/// Checks the chapters an analysis of a strategy's trade is told in (see the Trade part of <see cref="Explain"/>): its entry,
/// its exits and its outcome, written from the trade the backtest makes.
/// </summary>
[TestClass]
public class TradeExplainTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<Price> Candles(params (double Open, double High, double Low, double Close)[] candles) =>
        candles.Select((c, i) => new Price { DateTime = Start.AddHours(i), Open = c.Open, High = c.High, Low = c.Low, Close = c.Close }).ToList();

    // A red candle, then four green with bodies of 10, 20, 10 and 20 closing at 160: Take Profit 175, Stop Loss 145.
    private static readonly List<Price> Prices = Candles((101, 102, 99, 100), (100, 112, 99, 110), (110, 131, 109, 130), (130, 141, 129, 140), (140, 161, 139, 160),
        (160, 165, 158, 162), (162, 176, 160, 170));

    private static JsonNode StrategyJson => JsonNode.Parse("""
        { "id": "t", "name": "t", "direction": "Long", "entry": { "type": "SuccessiveCandles", "colour": "Green", "length": 4 },
          "takeProfit": { "type": "AverageCandle", "percent": 100, "candles": 4, "measure": "Body" },
          "stopLoss": { "type": "AverageCandle", "percent": 100, "candles": 4, "measure": "Body" } }
        """)!;

    private static List<string> Texts(JsonObject section, string level) => (section["cues"]?.AsArray() ?? [])
        .Select(x => x!["texts"]?[level]?.GetValue<string>()).Where(x => x != null).Select(x => x!).ToList();

    [TestMethod]
    public void Trade_TellsTheEntryExitsAndOutcome()
    {
        var section = Explain.Trade(Prices, 4, reached: 0, StrategyJson, null, out var problem);

        Assert.IsNotNull(section, problem);
        CollectionAssert.AreEqual(new[]
        {
            "The run begins with the candle at 01:00.",
            "The candle at 04:00 is the fourth green candle in a row, so the entry condition is met. The trade is entered at its close, buying at 160.",
            "Over the last four candles up to the entry, the average body, open to close, is 15.",
            "The Take Profit is that average, 15, above the entry, at 175.",
            "The Stop Loss is that average, 15, below it, at 145.",
            "Two hours later, at 06:00, the high of 176 reaches the Take Profit, so the trade closes at 175: it won 15, 9.38%.",
        }, Texts(section, "summary"));
        Assert.AreEqual(6, section["until"]!.GetValue<int>(), "The replay runs on to the exit.");
    }

    [TestMethod]
    public void Trade_NoTradeAtTheCandle_IsAProblem()
    {
        Assert.IsNull(Explain.Trade(Prices, 0, reached: 0, StrategyJson, null, out var problem));
        StringAssert.Contains(problem, "no trade at #0");
    }

    [TestMethod]
    public void Trade_AtACandleOfItsRunOrWhileOpen_IsThatTrade()
    {
        foreach (var at in new[] { 1, 3, 5, 6 })
        {
            var section = Explain.Trade(Prices, at, reached: 0, StrategyJson, null, out var problem);

            Assert.IsNotNull(section, $"#{at}: {problem}");
            Assert.AreEqual("The run begins with the candle at 01:00.", Texts(section, "summary")[0], $"#{at}");
        }
    }

    [TestMethod]
    public void Tour_OfATrade_HasAChapterForEachPart()
    {
        var strategy = Explain.StrategyOf(StrategyJson, out _);

        var tour = Explain.Tour([new Explain.Thing("Trade", null, "Trade")], "test", Prices, Prices[4].DateTime.ToString("yyyy-MM-ddTHH:mm"), null, out var missing, strategy: strategy);

        Assert.AreEqual(0, missing.Count);
        CollectionAssert.AreEqual(new[] { "Entry", "Exits", "Outcome" }, tour!["sections"]!.AsArray().Select(x => x!["chapter"]!.GetValue<string>()).ToArray());
        var compiled = Tours.Compile(tour, Prices);
        Assert.AreEqual(0, compiled.Errors.Count, string.Join("\n", compiled.Errors));
    }
}
