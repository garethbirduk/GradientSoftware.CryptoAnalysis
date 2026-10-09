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
            "Fourth of four Successive Green Candles, 01:00 to 04:00.",
            "That meets the entry condition, so the trade is entered at the close of the candle at 04:00, buying at 160.",
            "Over the last four candles up to the entry, the average body, open to close, is 15.",
            "The Take Profit is that average, 15, above the entry, at 175.",
            "The Stop Loss is that average, 15, below the entry, at 145.",
            "With 15 to win and 15 to lose, the Take Profit is a 1R target.",
            "Two hours later, at 06:00, the high of 176 reaches the Take Profit, so the trade closes at 175: it won 15, 9.38%.",
        }, Texts(section, "summary"));
        Assert.AreEqual(6, section["until"]!.GetValue<int>(), "The replay runs on to the exit.");
    }

    [TestMethod]
    public void Trade_Entry_CitesTheTermsDefinitions()
    {
        var section = Explain.Trade(Prices, 4, reached: 0, StrategyJson, ["Entry"], out var problem);

        Assert.IsNotNull(section, problem);
        var cited = (section["cues"]?.AsArray() ?? []).Select(x => x!["definition"]?.GetValue<string>()).Where(x => x != null).ToList();
        CollectionAssert.AreEqual(new[] { "Trade.entry", "Candle.colour", "SuccessiveCandles.what" }, cited);
    }

    [TestMethod]
    public void Trade_Entry_TellsTheRunAsItStoodAtTheEntry()
    {
        // The run goes on to a fifth green candle, and the page's setting would want five; the entry is told at its fourth.
        var section = Explain.Trade(Prices, 4, reached: 6, StrategyJson, ["Entry"], out var problem);

        Assert.IsNotNull(section, problem);
        Assert.AreEqual("Fourth of four Successive Green Candles, 01:00 to 04:00.", Texts(section, "summary")[0]);
    }

    [TestMethod]
    public void Trade_Entry_OfAChain_TellsEachStepAsItWasMet()
    {
        // Two red candles, then three green closing at 110, then a candle up to 121.
        var prices = Candles((100, 101, 89, 90), (90, 91, 79, 80), (80, 91, 79, 90), (90, 101, 89, 100), (100, 111, 99, 110), (110, 121, 108, 115));
        var strategy = JsonNode.Parse("""
            { "id": "c", "name": "c", "direction": "Long",
              "after": [ { "type": "SuccessiveCandles", "colour": "Red", "length": 2 } ],
              "entry": { "type": "SuccessiveCandles", "colour": "Green", "length": 3, "within": 5 },
              "takeProfit": { "type": "AverageCandle", "percent": 100, "candles": 3, "measure": "Body" },
              "stopLoss": { "type": "AverageCandle", "percent": 100, "candles": 3, "measure": "Body" } }
            """)!;

        var section = Explain.Trade(prices, 4, reached: 0, strategy, ["Entry"], out var problem);

        Assert.IsNotNull(section, problem);
        CollectionAssert.AreEqual(new[]
        {
            "Second of two Successive Red Candles, 00:00 to 01:00.",
            "That meets the first step, so the strategy now looks for three Successive Green Candles within five candles.",
            "Third of three Successive Green Candles, 02:00 to 04:00.",
            "That meets the entry condition, so the trade is entered at the close of the candle at 04:00, buying at 110.",
        }, Texts(section, "summary"));
        var cited = (section["cues"]?.AsArray() ?? []).Select(x => x!["definition"]?.GetValue<string>()).Where(x => x != null).ToList();
        CollectionAssert.AreEqual(new[] { "Trade.entry", "Trade.chain", "Candle.colour", "SuccessiveCandles.what" }, cited, "Each definition is taught once.");
        CollectionAssert.Contains(Texts(section, "education"),
            "This strategy's entry condition is two Successive Red Candles, then three Successive Green Candles within five candles. A run counts on the candle that makes its count, once a run, so a longer run does not count again.");
    }

    [TestMethod]
    public void Trade_Exits_TellAWickLevelThenTheirOwnMeasure()
    {
        var strategy = JsonNode.Parse("""
            { "id": "w", "name": "w", "direction": "Long", "entry": { "type": "SuccessiveCandles", "colour": "Green", "length": 4 },
              "takeProfit": { "type": "HighestWick", "candles": 4 },
              "stopLoss": { "type": "AverageCandle", "percent": 100, "candles": 4, "measure": "Body" } }
            """)!;

        var section = Explain.Trade(Prices, 4, reached: 0, strategy, ["TakeProfit", "StopLoss"], out var problem);

        Assert.IsNotNull(section, problem);
        CollectionAssert.AreEqual(new[]
        {
            "Over the last four candles up to the entry, the highest high is 161.",
            "The Take Profit is 1 above the entry, at 161.",
            "Over the last four candles up to the entry, the average body, open to close, is 15.",
            "The Stop Loss is that average, 15, below the entry, at 145.",
        }, Texts(section, "summary"));
    }

    [TestMethod]
    public void Trade_StopLossSetFromTheRisk_IsToldAfterTheRiskThatSetsIt()
    {
        var strategy = JsonNode.Parse("""
            { "id": "r", "name": "r", "direction": "Long", "entry": { "type": "SuccessiveCandles", "colour": "Green", "length": 4 },
              "takeProfit": { "type": "AverageCandle", "percent": 100, "candles": 4, "measure": "Body" },
              "stopLoss": { "type": "RiskRatio", "ratio": 2 } }
            """)!;

        var section = Explain.Trade(Prices, 4, reached: 0, strategy, ["TakeProfit", "StopLoss", "Risk"], out var problem);

        Assert.IsNotNull(section, problem);
        CollectionAssert.AreEqual(new[]
        {
            "Over the last four candles up to the entry, the average body, open to close, is 15.",
            "The Take Profit is that average, 15, above the entry, at 175.",
            "The Stop Loss is set for a 2R target: with 15 to win, the risk is 15 divided by 2, 7.",
            "The Stop Loss is 7 below the entry, at 152.",
        }, Texts(section, "summary"));
        var tour = Explain.Tour([new Explain.Thing("Trade", null, "Trade")], "test", Prices, Prices[4].DateTime.ToString("yyyy-MM-ddTHH:mm"), null, out _,
            strategy: Explain.StrategyOf(strategy, out _));
        CollectionAssert.AreEqual(new[] { "Entry", "Take Profit", "Risk", "Stop Loss", "Outcome" }, tour!["sections"]!.AsArray().Select(x => x!["chapter"]!.GetValue<string>()).ToArray(),
            "Risk that sets the Stop Loss is a condition of it, so it comes before it.");
    }

    [TestMethod]
    public void Risk_FromInputsOfItsOwn_TellsRewardRiskAndR()
    {
        // At #2, closing at 130: a Take Profit at 150 and a Stop Loss at 120.
        var section = Explain.Risk(Prices, 2, reached: 2, JsonNode.Parse("""{ "takeProfit": 150, "stopLoss": 120 }"""), null, out var problem);

        Assert.IsNotNull(section, problem);
        CollectionAssert.AreEqual(new[]
        {
            "The reward is 20, from the entry at 130 to the Take Profit at 150. The risk is 10, to the Stop Loss at 120.",
            "The Take Profit is a 2R target.",
        }, Texts(section, "summary"));
        var cited = (section["cues"]?.AsArray() ?? []).Select(x => x!["definition"]?.GetValue<string>()).Where(x => x != null).ToList();
        CollectionAssert.AreEqual(new[] { "Risk.what", "Risk.ratio", "Risk.breakeven" }, cited);
        Assert.AreEqual("At 2R, it breaks even over many trades when it wins 33% of them.", Texts(section, "education")[^1], "The share to break even is taught, not told as a fact of the trade.");
    }

    [TestMethod]
    public void Expand_ARiskSection_TellsItFromItsInputs()
    {
        var node = JsonNode.Parse("""{ "explain": "Risk", "at": 2, "risk": { "takeProfit": 150, "stopLoss": 120 } }""")!.AsObject();
        var errors = new List<string>();

        var section = Explain.Expand(node, Prices, 2, "section 1", errors);

        Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
        Assert.IsNull(section["risk"], "The inputs are taken out, as explain and at are.");
        Assert.AreEqual("The Take Profit is a 2R target.", Texts(section, "summary")[1]);
    }

    [TestMethod]
    public void Risk_ExitsOnOneSide_IsAProblem()
    {
        Assert.IsNull(Explain.Risk(Prices, 2, reached: 2, JsonNode.Parse("""{ "takeProfit": 150, "stopLoss": 140 }"""), null, out var problem));
        StringAssert.Contains(problem, "either side of the entry");
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
            Assert.AreEqual("Fourth of four Successive Green Candles, 01:00 to 04:00.", Texts(section, "summary")[0], $"#{at}");
        }
    }

    [TestMethod]
    public void Tour_OfATrade_HasAChapterForEachPart()
    {
        var strategy = Explain.StrategyOf(StrategyJson, out _);

        var tour = Explain.Tour([new Explain.Thing("Trade", null, "Trade")], "test", Prices, Prices[4].DateTime.ToString("yyyy-MM-ddTHH:mm"), null, out var missing, strategy: strategy);

        Assert.AreEqual(0, missing.Count);
        CollectionAssert.AreEqual(new[] { "Entry", "Take Profit", "Stop Loss", "Risk", "Outcome" }, tour!["sections"]!.AsArray().Select(x => x!["chapter"]!.GetValue<string>()).ToArray(),
            "Risk that sets neither exit is an observation of what they make, after both.");
        CollectionAssert.AreEqual(new[] { "What is an entry condition?", "What is a Take Profit?", "What is a Stop Loss?", "What is risk?", "How does a trade end?" },
            tour["sections"]!.AsArray().Select(x => x!["explainChapter"]!.GetValue<string>()).ToArray());
        var compiled = Tours.Compile(tour, Prices);
        Assert.AreEqual(0, compiled.Errors.Count, string.Join("\n", compiled.Errors));
    }
}
