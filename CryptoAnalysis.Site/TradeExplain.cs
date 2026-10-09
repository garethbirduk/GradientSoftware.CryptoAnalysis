using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gradient.CryptoAnalysis.Strategies;

namespace Gradient.CryptoAnalysis.Site;

/// <summary>
/// The Trade an analysis tells of: a strategy's trade entered at a candle, in chapters. Entry: what the entry condition is
/// and how this candle met it. Take Profit and Stop Loss: where each exit was set at the entry, and how it was worked out.
/// Risk: what the two exits make of the trade, by the Risk term's own block (see <see cref="RiskOf"/>). Outcome: the candle
/// that ended the trade, which exit it reached, and what it won or lost. Each is told at Education (what such a thing is)
/// and at Summary (this one), as the other things are. Risk is told where its role puts it (see <see cref="TradeOrder"/>):
/// just before the exit it sets, as a condition of that exit, or after both, as an observation of what they make.
/// </summary>
public static partial class Explain
{
    /// <summary>
    /// The chapters of a Trade, as blocks of one section each, with the chapter name of each and of its explanation.
    /// </summary>
    private static readonly (string Name, string Chapter, string Explains, Func<TradeRead, Block> Tell)[] TradeBlocks =
    [
        ("Entry", "Entry", "What is an entry condition?", TradeEntry),
        ("TakeProfit", "Take Profit", "What is a Take Profit?", r => TradeExit(r, profit: true)),
        ("StopLoss", "Stop Loss", "What is a Stop Loss?", r => TradeExit(r, profit: false)),
        ("Risk", "Risk", "What is risk?", TradeRisk),
        ("Outcome", "Outcome", "How does a trade end?", TradeOutcome),
    ];

    // The order a strategy's trade is told in. Risk that sets an exit is a condition of it, so it comes just before that
    // exit, after the one it is worked out from; otherwise it is an observation of what both exits make, after them.
    private static IEnumerable<(string Name, string Chapter, string Explains, Func<TradeRead, Block> Tell)> TradeOrder(Strategy strategy)
    {
        string[] order = strategy.StopLoss.IsRiskCondition ? ["Entry", "TakeProfit", "Risk", "StopLoss", "Outcome"]
            : strategy.TakeProfit.IsRiskCondition ? ["Entry", "StopLoss", "Risk", "TakeProfit", "Outcome"]
            : ["Entry", "TakeProfit", "StopLoss", "Risk", "Outcome"];
        return order.Select(name => TradeBlocks.First(x => x.Name == name));
    }

    // A trade as its chapters tell it: the prices, the strategy and the trade the backtest made.
    private sealed record TradeRead(List<Price> Prices, Strategy Strategy, StrategyTrade Trade)
    {
        public bool Long => Trade.Direction == EnumTradeDirection.Long;

        public int First => Trade.FirstRead(Strategy);
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
        return trades.FirstOrDefault(x => x.EntryIndex == at)
            ?? trades.FirstOrDefault(x => x.EntryIndex < at && at <= x.ExitIndex)
            ?? trades.FirstOrDefault(x => x.FirstRead(strategy) <= at && at < x.EntryIndex);
    }

    // The chapters of a Trade entered at the candle: one section for each block, the strategy in each.
    private static List<JsonObject>? TradeChapters(Thing thing, List<Price> prices, int index, int cursor, Strategy? strategy)
    {
        if (strategy == null || TradeAt(prices, strategy, index) is not { } trade)
            return null;
        var read = new TradeRead(prices, strategy, trade);
        var strategyJson = JsonSerializer.SerializeToNode(strategy, StrategyBook.JsonOptions);
        return TradeOrder(strategy).Select(b => new JsonObject
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
        var told = TradeOrder(strategy).Where(x => blocks == null || blocks.Contains(x.Name)).Select(x => x.Tell(read) with { Name = x.Name }).ToArray();
        var last = told.SelectMany(x => x.Cues).Max(x => x["at"]!.GetValue<int>());
        // Every chapter frames the whole trade, from the first candle the entry condition reads to the one that ends it.
        var view = new[] { read.First, trade.ExitIndex };
        return Section(view, last > reached ? last : null, told);
    }

    // What an entry condition is and this one; then each of its steps, told by its term's own block as it stood at the
    // candle that met it, and what the strategy looks for next; then the candle that met the last, where the trade is entered.
    private static Block TradeEntry(TradeRead r)
    {
        var t = r.Trade;
        var steps = r.Strategy.Steps;
        var met = t.Met.Select(x => x.Index).Append(t.EntryIndex).ToList();
        var length = Length(r.Prices);
        var buys = r.Long ? "buying" : "selling";
        var cues = new List<JsonObject> { Define(r.First, "Trade.entry") };
        if (steps.Count > 1)
            cues.Add(Define(r.First, "Trade.chain"));
        cues.Add(Teach(r.First, ConditionText(r.Strategy)));
        for (var k = 0; k < steps.Count; k++)
        {
            cues.AddRange(StepCues(r.Prices, steps[k], met[k]));
            if (k < steps.Count - 1)
                cues.Add(Say(met[k], $"That meets the {Ordinal(k + 1)} step, so the strategy now looks for {StepWords(steps[k + 1])}{WithinWords(steps[k + 1])}."));
        }

        cues.Add(Say(t.EntryIndex, $"That meets the entry condition, so the trade is entered at the close of the candle at {Clock(t.EntryTime, length)}, {buys} at {Money(t.EntryPrice)}."));
        // A definition is taught once in the chapter, however many steps cite it.
        var taught = new HashSet<string>();
        return new Block(cues.Where(x => x["definition"] == null || taught.Add(x["texts"]!.ToJsonString())).ToList(), ["candles", "trade"]);
    }

    // A strategy's entry condition in words: its steps in order, each after the one before, and how a run is counted.
    private static string ConditionText(Strategy strategy)
    {
        var steps = strategy.Steps;
        if (steps.Count == 1)
            return $"This strategy's entry condition is {StepWords(steps[0])}. It enters on the {Ordinal(steps[0].Length)} of them, once a run, so a longer run does not enter again.";
        var chain = string.Join(", then ", steps.Select((x, k) => k == 0 ? StepWords(x) : StepWords(x) + WithinWords(x)));
        return $"This strategy's entry condition is {chain}. A run counts on the candle that makes its count, once a run, so a longer run does not count again.";
    }

    // A step as the condition names it: "four Successive Green Candles".
    private static string StepWords(StrategyEntry step) =>
        $"{Words(step.Length)} Successive {(step.Colour == EnumCandleColour.Green ? "Green" : "Red")} Candles";

    // How soon a step must follow the one before it, when the strategy says.
    private static string WithinWords(StrategyEntry step) => step.Within is int within ? $" within {Words(within)} candles" : "";

    // A step met at a candle, told by the blocks of the term it is made of, as the term stood at that candle: for a run,
    // what makes a candle green or red, from the first candle the step reads, then the run's own block.
    private static IEnumerable<JsonObject> StepCues(List<Price> prices, StrategyEntry step, int at)
    {
        yield return Define(Math.Max(0, at - step.Reads), "Candle.colour");
        if (RunAt(prices.GetRange(0, at + 1), at, step.Length) is { } run)
            foreach (var cue in RunOf(new RunRead(prices, run, step.Colour == EnumCandleColour.Green)).Cues)
                yield return cue;
    }

    // What a Take Profit or a Stop Loss is; then where this trade's is, and how it was worked out from the candles up to the
    // entry. A Stop Loss worked out from the same measure as the Take Profit does not tell the measure again, and one set
    // from the risk leaves the rule to the Risk chapter before it.
    private static Block TradeExit(TradeRead r, bool profit)
    {
        var t = r.Trade;
        var (target, price) = profit ? (r.Strategy.TakeProfit, t.TakeProfit) : (r.Strategy.StopLoss, t.StopLoss);
        var side = r.Long == profit ? "above" : "below";
        var cues = new List<JsonObject> { Define(t.EntryIndex, profit ? "Trade.takeProfit" : "Trade.stopLoss") };
        var (distance, measure) = TargetText(r, target, price);
        var told = profit ? null : TargetText(r, r.Strategy.TakeProfit, t.TakeProfit).Measure;
        if (measure != null && measure != told)
            cues.Add(Say(t.EntryIndex, measure));
        cues.Add(Say(t.EntryIndex, $"The {(profit ? "Take Profit" : "Stop Loss")} is {distance} {side} the entry, at {Money(price)}."));
        return new Block(cues, ["candles", "trade"]);
    }

    // How far a target is from the entry, in words, and the measure it was worked out from when it has one.
    private static (string Distance, string? Measure) TargetText(TradeRead r, StrategyTarget target, double price)
    {
        var t = r.Trade;
        // Set from the risk: the rule is the Risk chapter's, told before this one.
        if (target.Type == StrategyTarget.RiskRatio)
            return (Money(Math.Abs(price - t.EntryPrice)), null);

        if (target.IsLevel)
        {
            var extreme = target.Type == StrategyTarget.HighestWick ? "highest high" : "lowest low";
            return (Money(Math.Abs(price - t.EntryPrice)), $"Over the last {Words(target.Candles)} candles up to the entry, the {extreme} is {Money(price)}.");
        }

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
            Define(t.EntryIndex, "Trade.outcome"),
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

    // The trade's risk, by the Risk term's block, in the role the strategy gives it: the condition its Stop Loss is set by,
    // or an observation of what its two exits make.
    private static Block TradeRisk(TradeRead r)
    {
        var t = r.Trade;
        var stop = r.Strategy.StopLoss;
        var role = stop.IsRiskCondition ? EnumRiskRole.SetsStopLoss : EnumRiskRole.Observed;
        return RiskOf(new RiskRead(t.EntryIndex, t.EntryPrice, t.TakeProfit, t.StopLoss, stop.IsRiskCondition ? stop.Ratio : null, role, r.Long));
    }

    // What Risk is told as: on its own, with the amounts it is worked out from; the condition a trade's Stop Loss is set by,
    // told before the Stop Loss; or an observation of a trade's two exits, told after them.
    private enum EnumRiskRole
    {
        OnItsOwn,
        SetsStopLoss,
        Observed,
    }

    // A trade's prices as the Risk term tells them: its entry, its Take Profit and its Stop Loss, at a candle; the target in
    // R a strategy sets, when it sets one; the role it is told in; and whether the trade is long.
    private sealed record RiskRead(int At, double Entry, double TakeProfit, double StopLoss, double? Set = null, EnumRiskRole Role = EnumRiskRole.OnItsOwn, bool Long = true);

    /// <summary>
    /// The written parts of a section that explains risk from its own inputs: { "entry", "takeProfit", "stopLoss", "ratio" }
    /// (entry the close of the candle when not given; ratio the target in R a strategy set, when it did). With a strategy
    /// instead, the risk of its trade at the candle (see <see cref="Trade"/>). Null, with the problem, when neither gives a trade.
    /// </summary>
    public static JsonObject? Risk(List<Price> prices, int at, int reached, JsonNode? inputs, JsonNode? strategy, out string problem)
    {
        if (inputs == null)
            return Trade(prices, at, reached, strategy, ["Risk"], out problem);
        problem = "";
        double? Number(string key) => inputs[key] is JsonValue v && v.TryGetValue<double>(out var x) ? x : null;
        var entry = Number("entry") ?? prices[at].Close;
        if (Number("takeProfit") is not double takeProfit || Number("stopLoss") is not double stopLoss
            || takeProfit == entry || stopLoss == entry || (takeProfit > entry) == (stopLoss > entry))
        {
            problem = "risk is { \"entry\", \"takeProfit\", \"stopLoss\" }, with the Take Profit and the Stop Loss on either side of the entry";
            return null;
        }

        var block = RiskOf(new RiskRead(at, entry, takeProfit, stopLoss, Number("ratio"), EnumRiskRole.OnItsOwn, takeProfit > entry)) with { Name = "Risk" };
        return Section([Math.Max(0, at - CandlesBefore), Math.Min(prices.Count - 1, at + CandlesAfter)], at > reached ? at : null, block);
    }

    // What risk, reward and R are; then this trade's, as its role tells it; and, with the break-even rule, the share of trades
    // a strategy at that target must win, which teaches the rule and is not a fact of this trade.
    private static Block RiskOf(RiskRead r)
    {
        var reward = Math.Abs(r.TakeProfit - r.Entry);
        var risk = Math.Abs(r.Entry - r.StopLoss);
        var target = r.Set ?? reward / risk;
        var times = RText(target);
        var share = (100 / (1 + target)).ToString("0", CultureInfo.InvariantCulture);
        var (above, below) = r.Long ? ("above", "below") : ("below", "above");
        var cues = new List<JsonObject> { Define(r.At, "Risk.what") };
        if (r.Role == EnumRiskRole.OnItsOwn)
            cues.Add(Say(r.At, $"The reward is {Money(reward)}, from the entry at {Money(r.Entry)} to the Take Profit at {Money(r.TakeProfit)}. The risk is {Money(risk)}, to the Stop Loss at {Money(r.StopLoss)}."));
        cues.Add(Define(r.At, "Risk.ratio"));
        cues.Add(Say(r.At, r.Role switch
        {
            EnumRiskRole.SetsStopLoss when target == 1 => $"The Stop Loss is set for a 1R target: as far {below} the entry as the Take Profit is {above} it, {Money(reward)}.",
            EnumRiskRole.SetsStopLoss => $"The Stop Loss is set for a {times}R target: with {Money(reward)} to win, the risk is {Money(reward)} divided by {times}, {Money(risk)}.",
            EnumRiskRole.Observed => $"With {Money(reward)} to win and {Money(risk)} to lose, the Take Profit is a {times}R target.",
            _ => $"The Take Profit is a {times}R target{(r.Set != null ? ", as the strategy sets it" : "")}.",
        }));
        cues.Add(Define(r.At, "Risk.breakeven"));
        cues.Add(Teach(r.At, $"At {times}R, it breaks even over many trades when it wins {share}% of them."));
        return new Block(cues, ["candles", "trade"]);
    }

    // A number of R as it is said: to two places at most, "1", "0.5", "1.25".
    private static string RText(double ratio) => Math.Round(ratio, 2).ToString("0.##", CultureInfo.InvariantCulture);

    private static string Target(EnumTradeOutcome outcome) => outcome == EnumTradeOutcome.TakeProfit ? "Take Profit" : "Stop Loss";
}
