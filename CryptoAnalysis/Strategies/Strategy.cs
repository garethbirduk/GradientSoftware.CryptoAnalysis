using System.Text.Json;
using System.Text.Json.Serialization;
using Gradient.CryptoAnalysis.Conditions.PriceConditions;
using Gradient.CryptoAnalysis.Research;

namespace Gradient.CryptoAnalysis.Strategies;

public enum EnumTradeDirection
{
    Long,
    Short,
}

public enum EnumCandleColour
{
    Green,
    Red,
}

/// <summary>
/// What a candle is measured by: its body (open to close) or its whole range (low to high).
/// </summary>
public enum EnumCandleMeasure
{
    Body,
    Range,
}

/// <summary>
/// A strategy as the Strategies page edits it and strategies.json keeps it: when to enter, which way, and where the take
/// profit and stop loss are. Each part has a type, so new kinds of entry and target can be added alongside the first ones.
/// The entry condition can be a chain: the steps of After, in order, then Entry, each looked for only once the step before
/// it is met (see <see cref="StrategyEntry.Within"/>). Entry is the step that enters.
/// </summary>
public sealed class Strategy
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public EnumTradeDirection Direction { get; set; } = EnumTradeDirection.Long;

    /// <summary>
    /// The steps the entry condition must meet first, in order, before Entry; none for an entry of one step.
    /// </summary>
    public List<StrategyEntry> After { get; set; } = [];

    public StrategyEntry Entry { get; set; } = new();

    /// <summary>
    /// The steps of the entry condition in order: those of After, then Entry.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<StrategyEntry> Steps => [.. After, Entry];

    public StrategyTarget TakeProfit { get; set; } = new();

    public StrategyTarget StopLoss { get; set; } = new();

    /// <summary>
    /// With this set, no trade is entered while another is open.
    /// </summary>
    public bool OnePositionAtATime { get; set; } = true;

    /// <summary>
    /// The fee on each side of a trade, entry and exit, as a percentage of its value there; none when not set.
    /// </summary>
    public double FeePercent { get; set; }

    /// <summary>
    /// How far a market order fills against the trade, as a percentage of its price: the entry, at the close, and a Stop
    /// Loss, which becomes a market order once reached. A Take Profit is a limit order, so it fills at its price. None when
    /// not set.
    /// </summary>
    public double SlippagePercent { get; set; }

    /// <summary>
    /// Whether the fee is counted; off, the strategy keeps its FeePercent but pays none.
    /// </summary>
    public bool CountFees { get; set; } = true;

    /// <summary>
    /// Whether slippage is counted; off, the strategy keeps its SlippagePercent but none is taken.
    /// </summary>
    public bool CountSlippage { get; set; } = true;

    /// <summary>
    /// The numbers a sweep varies (see <see cref="StrategySweep"/>), each by its path in the strategy's JSON, as
    /// "stopLoss.ratio" or "after.0.length": from the value the strategy has, up to To in steps of Step. A run or a baseline
    /// takes the strategy as it is, with each number at its own value.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, StrategyRange>? Vary { get; set; }

    /// <summary>
    /// What is wrong with the strategy, as messages; empty when it can be run.
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Id))
            errors.Add("The strategy has no id.");
        errors.AddRange(After.SelectMany((step, k) => step.Validate().Select(x => $"Step {k + 1}: {x}")));
        errors.AddRange(Entry.Validate().Select(x => $"Entry: {x}"));
        if (Steps[0].Within != null)
            errors.Add($"{(After.Count > 0 ? "Step 1" : "Entry")}: Within needs a step before it.");
        if (Entry.Kill is { Count: > 0 })
            errors.Add("Entry: a kill ends the wait that follows a step, and nothing is waited for after the entry.");
        errors.AddRange(TakeProfit.Validate().Select(x => $"Take Profit: {x}"));
        if (TakeProfit.Type == StrategyTarget.RiskRatio)
            errors.Add("Take Profit: a target in R sets the Stop Loss from the Take Profit, so only the Stop Loss can be one.");
        errors.AddRange(StopLoss.Validate().Select(x => $"Stop Loss: {x}"));
        if ((TakeProfit.IsRange || StopLoss.IsRange) && !Steps.Any(x => x.IsRange))
            errors.Add("A target on a Range's lines needs a Range step in the entry condition.");
        if (FeePercent < 0)
            errors.Add("The fee cannot be less than 0.");
        if (SlippagePercent < 0)
            errors.Add("The slippage cannot be less than 0.");
        return errors;
    }
}

/// <summary>
/// Which Swings a Swing step is met by: any, only a Strong one (no market structure break inside it), or only a Weak one.
/// </summary>
public enum EnumSwingStrength
{
    Any,
    Strong,
    Weak,
}

/// <summary>
/// A step of an entry condition: what is met at the close of a candle. SuccessiveCandles is met at the close of the candle
/// that makes a run of Length candles of one colour, once a run, so a longer run does not meet it again.
/// The others are market structure at a sawtooth Level, read as a chart of the last candles to that one shows it (see
/// <see cref="KnownStructure"/>), in a Direction: up for bullish, an Upswing or an Uptrend. BreakOfStructure and
/// MarketStructureBreak are met at the close that breaks. Swing is met at the break of structure that Confirms a Swing, of
/// the Strength asked for. Trend is met at the break of structure that makes Swings Swings in a row, once a run, so two is
/// the Trend being Confirmed and a longer Trend does not meet it again.
/// RangeZone is met at the close of a candle that reaches into a zone of a Range holding at the Level (see
/// <see cref="KnownRanges"/>): the buy-low zone, from the lower band to Depth percent of the Range's height above its low, or
/// the sell-high zone, as far below its high up to the upper band. The Range is one whose Retracement read at least
/// Retracement percent, with bands of Band percent of its height. The chain holds that Range from then on, by its lines.
/// RangeSweep is met at the close of a candle that sweeps such a Range: on the BuyLow side its wick goes below the Range's
/// low and it closes at or above it, back inside the box; on the SellHigh side its wick goes above the high and it closes at
/// or below it. It holds the Range as a RangeZone does, with Depth the depth of the zones its exits can be set on.
/// A step can have kills: conditions of these same types that, met at a close after the step is met and before the trade is
/// entered, end the wait, so the chain starts again. RangeEnd is a kill alone, and of a Range step: the close beyond a
/// band of the Range that step holds.
/// </summary>
public sealed class StrategyEntry
{
    public const string SuccessiveCandles = nameof(SuccessiveCandles);
    public const string BreakOfStructure = nameof(BreakOfStructure);
    public const string MarketStructureBreak = nameof(MarketStructureBreak);
    public const string Swing = nameof(Swing);
    public const string Trend = nameof(Trend);
    public const string RangeZone = nameof(RangeZone);
    public const string RangeSweep = nameof(RangeSweep);
    public const string RangeEnd = nameof(RangeEnd);
    public static readonly string[] Types = [SuccessiveCandles, BreakOfStructure, MarketStructureBreak, Swing, Trend, RangeZone, RangeSweep];
    public static readonly string[] KillTypes = [.. Types, RangeEnd];

    public string Type { get; set; } = SuccessiveCandles;

    public EnumCandleColour Colour { get; set; } = EnumCandleColour.Green;

    public int Length { get; set; } = 4;

    public EnumSwingDirection Direction { get; set; } = EnumSwingDirection.Up;

    public int Level { get; set; } = 2;

    public int Swings { get; set; } = 2;

    public EnumSwingStrength Strength { get; set; } = EnumSwingStrength.Any;

    public EnumRangeZone Zone { get; set; } = EnumRangeZone.BuyLow;

    /// <summary>
    /// How deep a Range's zone is from its low or high, as a percentage of its height.
    /// </summary>
    public double Depth { get; set; } = 25;

    /// <summary>
    /// The least a Range's Retracement read when it was identified, as a percentage.
    /// </summary>
    public double Retracement { get; set; } = Ranges.DefaultMinRetracement;

    /// <summary>
    /// How far beyond a Range's low and high its bands are, as a percentage of its height.
    /// </summary>
    public double Band { get; set; } = Ranges.DefaultBand;

    /// <summary>
    /// The conditions that end the wait this step begins; none when not set.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<StrategyEntry>? Kill { get; set; }

    /// <summary>
    /// Whether the step is a break of market structure rather than a run of candles or a Range.
    /// </summary>
    [JsonIgnore]
    public bool IsStructure => Type is BreakOfStructure or MarketStructureBreak or Swing or Trend;

    /// <summary>
    /// Whether the step is met in a Range, a reach into its zone or a sweep of it, which the chain holds the Range of from
    /// then on.
    /// </summary>
    [JsonIgnore]
    public bool IsRange => Type is RangeZone or RangeSweep;

    /// <summary>
    /// Whether the step is read from a chart of the last candles to its candle, so its Level is that chart's.
    /// </summary>
    [JsonIgnore]
    public bool ReadsChart => IsStructure || IsRange;

    /// <summary>
    /// The most candles from the candle that met the step before to the one that meets this; no limit when not set.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Within { get; set; }

    /// <summary>
    /// How many candles before the candle that meets the step must come after the step before it: a run's candles before
    /// its Length-th. A break is met by its own candle alone, though the Swing or Trend it is read from began before.
    /// </summary>
    [JsonIgnore]
    public int Reads => Type == SuccessiveCandles ? Math.Max(0, Length - 1) : 0;

    /// <summary>
    /// What is wrong with the entry, as messages; empty when it can be run.
    /// </summary>
    public List<string> Validate() => Validate(kill: false);

    private List<string> Validate(bool kill)
    {
        if (!(kill ? KillTypes : Types).Contains(Type))
            return [$"\"{Type}\" is not {(kill ? "a kill" : "an entry")} type: {string.Join(", ", kill ? KillTypes : Types)}."];
        var errors = new List<string>();
        if (Type == SuccessiveCandles && Length < 1)
            errors.Add("Length must be at least 1.");
        if (ReadsChart && Level is < 1 or > KnownStructure.Levels)
            errors.Add($"Level must be from 1 to {KnownStructure.Levels}.");
        if (Type == Trend && Swings < 2)
            errors.Add("Swings must be at least 2: a Trend is two or more Swings in a row.");
        if (IsRange && Depth is <= 0 or > 50)
            errors.Add("Depth must be more than 0 and no more than 50: the zones are either side of the Range's middle.");
        if (IsRange && Retracement is <= 0 or >= 100)
            errors.Add("Retracement must be more than 0 and less than 100: at 100 the Retracement is an MSB.");
        if (IsRange && Band < 0)
            errors.Add("Band cannot be less than 0.");
        if (Within < 1)
            errors.Add("Within must be at least 1 candle.");
        if (kill && (Within != null || Kill != null))
            errors.Add("A kill has no Within or kills of its own.");
        if (!IsRange && Kill?.Any(x => x.Type == RangeEnd) == true)
            errors.Add("A Range's end is a kill of the Range step whose Range it ends.");
        if (!kill)
            errors.AddRange((Kill ?? []).SelectMany((x, k) => x.Validate(kill: true).Select(e => $"Kill {k + 1}: {e}")));
        return errors;
    }
}

/// <summary>
/// Where a take profit or stop loss is. Some types are a distance from the entry: AverageCandle is Percent of the average
/// size of the last Candles candles up to the entry candle, measured by Measure; Percent is Percent of the entry price.
/// Others are a price level: HighestWick is the highest high of the last Candles candles up to the entry candle, and
/// LowestWick the lowest low. RiskRatio, for a stop loss only, makes the take profit a target of Ratio R: the stop loss is
/// the reward (the take profit's distance from the entry) divided by Ratio, so the risk, 1R, is that far.
/// RangeZone and RangeBand are lines of the Range the entry condition holds (see <see cref="HeldRange"/>), on the target's
/// side of the entry: RangeZone the near edge of the zone there, the bottom of the sell-high zone above or the top of the
/// buy-low zone below, and RangeBand the band there.
/// </summary>
public sealed class StrategyTarget
{
    public const string AverageCandle = nameof(AverageCandle);
    public const string Percent = nameof(Percent);
    public const string HighestWick = nameof(HighestWick);
    public const string LowestWick = nameof(LowestWick);
    public const string RiskRatio = nameof(RiskRatio);
    public const string RangeZone = nameof(RangeZone);
    public const string RangeBand = nameof(RangeBand);
    public static readonly string[] Types = [AverageCandle, Percent, HighestWick, LowestWick, RiskRatio, RangeZone, RangeBand];

    /// <summary>
    /// Whether the target is a price level of its own rather than a distance from the entry.
    /// </summary>
    [JsonIgnore]
    public bool IsLevel => Type is HighestWick or LowestWick || IsRange;

    /// <summary>
    /// Whether the target is a line of the Range the entry condition holds.
    /// </summary>
    [JsonIgnore]
    public bool IsRange => Type is RangeZone or RangeBand;

    /// <summary>
    /// Whether the target is set from the risk, so the risk is a condition of it rather than an observation of the exits.
    /// </summary>
    [JsonIgnore]
    public bool IsRiskCondition => Type == RiskRatio;

    public string Type { get; set; } = AverageCandle;

    [JsonPropertyName("percent")]
    public double Percentage { get; set; } = 100;

    public int Candles { get; set; } = 4;

    public EnumCandleMeasure Measure { get; set; } = EnumCandleMeasure.Body;

    /// <summary>
    /// The take profit's target in R for a RiskRatio stop loss: the reward divided by the risk, 2 for a 2R target.
    /// </summary>
    public double Ratio { get; set; } = 1;

    /// <summary>
    /// What is wrong with the target, as messages; empty when it can be run.
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();
        if (!Types.Contains(Type))
            errors.Add($"\"{Type}\" is not a target type: {string.Join(", ", Types)}.");
        if (Percentage <= 0)
            errors.Add("The percentage must be more than 0.");
        if (Type is AverageCandle or HighestWick or LowestWick && Candles < 1)
            errors.Add("Candles must be at least 1.");
        if (Type == RiskRatio && Ratio <= 0)
            errors.Add("The target in R must be more than 0.");
        return errors;
    }

    /// <summary>
    /// The price of the target for a trade entered at the close of candle index, on the side of the entry the target is on:
    /// above it for a Long's take profit and a Short's stop loss, below it for the others. Null when it cannot be set there:
    /// too few candles before it to measure, or a level that is not beyond the entry on that side. A RiskRatio stop loss is
    /// set from the take profit's price, which takeProfit gives, and a line of a Range from the Range the entry condition
    /// holds, which range gives.
    /// </summary>
    public double? Price(IReadOnlyList<Price> prices, int index, EnumTradeDirection direction, bool profit, double? takeProfit = null, HeldRange? range = null)
    {
        var entry = prices[index].Close;
        var side = (direction == EnumTradeDirection.Long) == profit ? 1 : -1;
        if (Type == RiskRatio)
            return takeProfit is double reward && reward != entry ? entry + side * Math.Abs(reward - entry) / Ratio : null;
        if (IsRange)
            return range != null && Line(range, side > 0) is var line && (line - entry) * side > 0 ? line : null;
        if (IsLevel)
            return Level(prices, index) is double level && (level - entry) * side > 0 ? level : null;
        return Distance(prices, index) is double distance && distance > 0 ? entry + side * distance : null;
    }

    /// <summary>
    /// The line of a Range a RangeZone or RangeBand target is at, above the Range's middle or below it.
    /// </summary>
    public double Line(HeldRange range, bool above) =>
        Type == RangeZone ? (above ? range.SellHighBottom : range.BuyLowTop) : above ? range.BandHigh : range.BandLow;

    /// <summary>
    /// The price level a HighestWick or LowestWick target is at for a trade entered at the close of candle index; null for
    /// the other types, or when there are not enough candles before it.
    /// </summary>
    public double? Level(IReadOnlyList<Price> prices, int index)
    {
        if (IsRange || !IsLevel || index - Candles + 1 < 0)
            return null;
        var candles = Enumerable.Range(index - Candles + 1, Candles);
        return Type == HighestWick ? candles.Max(i => prices[i].High) : candles.Min(i => prices[i].Low);
    }

    /// <summary>
    /// The distance from the entry price to an AverageCandle or Percent target for a trade entered at the close of candle
    /// index; null for a level, or when there are not enough candles before it to measure.
    /// </summary>
    public double? Distance(IReadOnlyList<Price> prices, int index)
    {
        if (IsLevel || Type == RiskRatio)
            return null;
        if (Type == Percent)
            return prices[index].Close * Percentage / 100;

        if (index - Candles + 1 < 0)
            return null;
        var sizes = Enumerable.Range(index - Candles + 1, Candles).Select(i => Measure == EnumCandleMeasure.Body
            ? Math.Abs(prices[i].Close - prices[i].Open)
            : prices[i].High - prices[i].Low);
        return sizes.Average() * Percentage / 100;
    }
}

/// <summary>
/// The Range an entry condition holds once a RangeZone step is met, as the lines a strategy trades it by: its Low and
/// High, its bands, a close beyond which ends it, and the edges of its zones, the top of the buy-low zone and the bottom
/// of the sell-high zone. Level is the chart's it was read at, From the candle its Retracement is measured from,
/// Identified the candle whose close identified it, and Met the candle that met the step.
/// </summary>
public sealed record HeldRange(int Level, EnumSwingDirection Direction, double Low, double High, double BandLow, double BandHigh,
    double BuyLowTop, double SellHighBottom, int Retracement, int From, int Identified, int Met)
{
    /// <summary>
    /// Whether a close ends the Range: one beyond either band.
    /// </summary>
    public bool EndedBy(double close) => close > BandHigh || close < BandLow;
}

/// <summary>
/// How a sweep varies one number of a strategy: from the strategy's own value up to To, in steps of Step.
/// </summary>
public sealed class StrategyRange
{
    public double To { get; set; }

    public double Step { get; set; }
}

/// <summary>
/// The costs the Strategies page gives a strategy that has none of its own: a new one, or one kept before it had costs.
/// </summary>
public sealed class StrategyCosts
{
    public double FeePercent { get; set; }

    public double SlippagePercent { get; set; }
}

/// <summary>
/// The strategies kept in strategies.json, with the costs a strategy is given by default.
/// </summary>
public sealed class StrategyBook
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StrategyCosts? Defaults { get; set; }

    public List<Strategy> Strategies { get; set; } = [];

    /// <summary>
    /// Reads strategies.json's text.
    /// </summary>
    public static StrategyBook Parse(string json) =>
        JsonSerializer.Deserialize<StrategyBook>(json, JsonOptions) ?? new StrategyBook();
}
