using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gradient.CryptoAnalysis.Strategies;

namespace Gradient.CryptoAnalysis.Site;

/// <summary>
/// The Trade an analysis tells of: a strategy's trade entered at a candle, in three chapters. Entry: what the entry
/// condition is and how this candle met it. Exits: the Take Profit and Stop Loss set at the entry, and how they were worked
/// out. Outcome: the candle that ended the trade, which target it reached, and what it won or lost. Each is told at
/// Education (what such a thing is) and at Summary (this one), as the other things are.
/// </summary>
public static partial class Explain
{
    /// <summary>
    /// The chapters of a Trade, as blocks of one section each, with the chapter name of each and of its explanation.
    /// </summary>
    private static readonly (string Name, string Chapter, string Explains, Func<TradeRead, Block> Tell)[] TradeBlocks =
    [
        ("Entry", "Entry", "What is an entry condition?", TradeEntry),
        ("Exits", "Exits", "What are the exits?", TradeExits),
        ("Outcome", "Outcome", "How does a trade end?", TradeOutcome),
    ];

    // A trade as its chapters tell it: the prices, the strategy and the trade the backtest made.
    private sealed record TradeRead(List<Price> Prices, Strategy Strategy, StrategyTrade Trade)
    {
        public bool Long => Trade.Direction == EnumTradeDirection.Long;

        public int First => Strategy.Entry.Type == StrategyEntry.SuccessiveCandles ? Math.Max(0, Trade.EntryIndex - Strategy.Entry.Length + 1) : Trade.EntryIndex;
    }

    /// <summary>
    /// The strategy a section or an address gives as JSON, in the shape strategies.json keeps it; null when there is none or
    /// it is not one, with the problem.
    /// </summary>
    public static Strategy? StrategyOf(JsonNode? node, out string problem)
    {
        problem = "";
        try
        {
            var strategy = node?.Deserialize<Strategy>(StrategyBook.JsonOptions);
            if (strategy == null)
            {
                problem = "a Trade needs the strategy that made it";
                return null;
            }

            var errors = strategy.Validate();
            problem = string.Join(" ", errors);
            return errors.Count == 0 ? strategy : null;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            problem = $"the strategy is not valid: {e.Message}";
            return null;
        }
    }

    // The trade of the strategy at the candle, as the backtest of the whole of the prices makes it: the one entered there,
    // else the one open there, else the one whose entry condition the candle is part of.
    private static StrategyTrade? TradeAt(List<Price> prices, Strategy strategy, int at)
    {
        var trades = StrategyBacktest.Run(prices, strategy).Trades;
        var reads = strategy.Entry.Type == StrategyEntry.SuccessiveCandles ? strategy.Entry.Length - 1 : 0;
        return trades.FirstOrDefault(x => x.EntryIndex == at)
            ?? trades.FirstOrDefault(x => x.EntryIndex < at && at <= x.ExitIndex)
            ?? trades.FirstOrDefault(x => x.EntryIndex - reads <= at && at < x.EntryIndex);
    }

    // The chapters of a Trade entered at the candle: one section for each block, the strategy in each.
    private static List<JsonObject>? TradeChapters(Thing thing, List<Price> prices, int index, int cursor, Strategy? strategy)
    {
        if (strategy == null || TradeAt(prices, strategy, index) is not { } trade)
            return null;
        var read = new TradeRead(prices, strategy, trade);
        var strategyJson = JsonSerializer.SerializeToNode(strategy, StrategyBook.JsonOptions);
        return TradeBlocks.Select(b => new JsonObject
        {
            ["chapter"] = b.Chapter,
            ["explainChapter"] = b.Explains,
            ["explain"] = thing.Kind,
            ["at"] = index,
            ["blocks"] = new JsonArray(b.Name),
            ["from"] = b.Name == "Entry" ? read.First : trade.EntryIndex,
            ["known"] = cursor,
            ["layers"] = Layers("candles", "trade"),
            ["speed"] = b.Name == "Outcome" ? Math.Clamp(trade.Candles / 4.0, 2, 40) : 4.0,
            ["strategy"] = strategyJson?.DeepClone(),
        }).ToList();
    }

    /// <summary>
    /// The written parts of a section that explains a strategy's trade entered at a candle, in the blocks it names (see
    /// <see cref="BlocksOf"/>). Null, with the problem, when the strategy makes no trade there.
    /// </summary>
    public static JsonObject? Trade(List<Price> prices, int at, int reached, JsonNode? strategyNode, IReadOnlyCollection<string>? blocks, out string problem)
    {
        if (StrategyOf(strategyNode, out problem) is not { } strategy)
            return null;
        if (TradeAt(prices, strategy, at) is not { } trade)
        {
            problem = $"the strategy enters no trade at #{at}";
            return null;
        }

        var read = new TradeRead(prices, strategy, trade);
        var told = TradeBlocks.Where(x => blocks == null || blocks.Contains(x.Name)).Select(x => x.Tell(read) with { Name = x.Name }).ToArray();
        var last = told.SelectMany(x => x.Cues).Max(x => x["at"]!.GetValue<int>());
        // Every chapter frames the whole trade, from the first candle the entry condition reads to the one that ends it.
        var view = new[] { read.First, trade.ExitIndex };
        return Section(view, last > reached ? last : null, told);
    }

    // What an entry condition is; then this one, and the candle that met it, where the trade is entered.
    private static Block TradeEntry(TradeRead r)
    {
        var t = r.Trade;
        var e = r.Strategy.Entry;
        var length = Length(r.Prices);
        var buys = r.Long ? "buying" : "selling";
        var colour = e.Colour == EnumCandleColour.Green ? "green" : "red";
        var cues = new List<JsonObject>
        {
            Teach(r.First, "A strategy enters a trade when its entry condition is met. The condition is read at the close of each candle, from that candle and the ones before it alone, and the trade is entered at that close."),
            Teach(r.First, $"This strategy's entry condition is {Words(e.Length)} {colour} candles in a row: each closes {(e.Colour == EnumCandleColour.Green ? "above" : "below")} its open. It enters on the {Ordinal(e.Length)} of them, once a run, so a longer run does not enter again."),
            Say(r.First, $"The run begins with the candle at {Clock(r.Prices[r.First].DateTime, length)}."),
            Say(t.EntryIndex, $"The candle at {Clock(t.EntryTime, length)} is the {Ordinal(e.Length)} {colour} candle in a row, so the entry condition is met. The trade is entered at its close, {buys} at {Money(t.EntryPrice)}."),
        };
        return new Block(cues, ["candles", "trade"]);
    }

    // What the exits are; then where this trade's are, and how they were worked out from the candles up to the entry.
    private static Block TradeExits(TradeRead r)
    {
        var t = r.Trade;
        var (above, below) = r.Long ? ("above", "below") : ("below", "above");
        var cues = new List<JsonObject>
        {
            Teach(t.EntryIndex, $"A trade is given two exits as it is entered: a Take Profit {above} the entry, where it closes with a gain, and a Stop Loss {below} it, where it closes with a loss. Whichever the price reaches first ends the trade."),
        };
        var profit = TargetText(r, r.Strategy.TakeProfit);
        var loss = TargetText(r, r.Strategy.StopLoss);
        if (profit.Measure != null)
            cues.Add(Say(t.EntryIndex, profit.Measure));
        if (loss.Measure != null && loss.Measure != profit.Measure)
            cues.Add(Say(t.EntryIndex, loss.Measure));
        cues.Add(Say(t.EntryIndex, $"The Take Profit is {profit.Distance} {above} the entry, at {Money(t.TakeProfit)}."));
        cues.Add(Say(t.EntryIndex, $"The Stop Loss is {loss.Distance} {below} it, at {Money(t.StopLoss)}."));
        return new Block(cues, ["candles", "trade"]);
    }

    // How far a target is from the entry, in words, and the measure it was worked out from when it has one.
    private static (string Distance, string? Measure) TargetText(TradeRead r, StrategyTarget target)
    {
        var t = r.Trade;
        var amount = target.Distance(r.Prices, t.EntryIndex) ?? 0;
        var percent = target.Percentage.ToString("0.##", CultureInfo.InvariantCulture);
        if (target.Type == StrategyTarget.Percent)
            return ($"{percent}% of the entry price, {Money(amount)}", null);
        var size = target.Measure == EnumCandleMeasure.Range ? "range, high to low," : "body, open to close,";
        var average = amount * 100 / target.Percentage;
        var measure = $"Over the last {Words(target.Candles)} candles up to the entry, the average {size} is {Money(average)}.";
        return (target.Percentage == 100 ? $"that average, {Money(amount)}," : $"{percent}% of that average, {Money(amount)},", measure);
    }

    // How a trade ends; then the candle that ended this one, which target it reached, and what it won or lost.
    private static Block TradeOutcome(TradeRead r)
    {
        var t = r.Trade;
        var length = Length(r.Prices);
        var exit = r.Prices[t.ExitIndex];
        var (high, low) = r.Long ? ("high", "low") : ("low", "high");
        var cues = new List<JsonObject>
        {
            Teach(t.EntryIndex, $"From the next candle on, each candle is checked against the exits. When its {high} reaches the Take Profit, the trade closes there with a gain; when its {low} reaches the Stop Loss, it closes there with a loss. A candle that opens beyond an exit closes the trade at its open. A candle that reaches both cannot say which came first, so it counts as the Stop Loss."),
        };
        var later = $"{Capital(Duration(t.Candles, length))} later, at {Clock(t.ExitTime, length)}";
        var result = t.Profit >= 0
            ? $"it won {Money(Math.Abs(t.Profit))}, {Math.Abs(t.ProfitPercent).ToString("0.00", CultureInfo.InvariantCulture)}%"
            : $"it lost {Money(Math.Abs(t.Profit))}, {Math.Abs(t.ProfitPercent).ToString("0.00", CultureInfo.InvariantCulture)}%";
        var gapped = t.ExitPrice == exit.Open && t.Outcome != EnumTradeOutcome.Open && t.ExitPrice != (t.Outcome == EnumTradeOutcome.TakeProfit ? t.TakeProfit : t.StopLoss);
        var reached = r.Long ? (exit.High, exit.Low) : (exit.Low, exit.High);
        var said = t.Outcome switch
        {
            EnumTradeOutcome.Open => $"The prices end with the trade still open. At the last close, {Money(t.ExitPrice)}, {result.Replace("it won", "it is up").Replace("it lost", "it is down")}.",
            _ when gapped => $"{later}, the candle opens at {Money(exit.Open)}, already beyond the {Target(t.Outcome)}, so the trade closes at that open: {result}.",
            _ when t.BothTouched => $"{later}, one candle reaches both exits: its {high} of {Money(reached.Item1)} passes the Take Profit and its {low} of {Money(reached.Item2)} the Stop Loss. It cannot say which came first, so it counts as the Stop Loss: the trade closes at {Money(t.ExitPrice)}, and {result}.",
            EnumTradeOutcome.TakeProfit => $"{later}, the {high} of {Money(reached.Item1)} reaches the Take Profit, so the trade closes at {Money(t.ExitPrice)}: {result}.",
            _ => $"{later}, the {low} of {Money(reached.Item2)} reaches the Stop Loss, so the trade closes at {Money(t.ExitPrice)}: {result}.",
        };
        cues.Add(Say(t.ExitIndex, said));
        return new Block(cues, ["candles", "trade"]);
    }

    private static string Target(EnumTradeOutcome outcome) => outcome == EnumTradeOutcome.TakeProfit ? "Take Profit" : "Stop Loss";
}
