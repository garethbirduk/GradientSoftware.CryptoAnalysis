using System.Text.Json;
using System.Text.Json.Serialization;

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
        errors.AddRange(TakeProfit.Validate().Select(x => $"Take Profit: {x}"));
        if (TakeProfit.Type == StrategyTarget.RiskRatio)
            errors.Add("Take Profit: a target in R sets the Stop Loss from the Take Profit, so only the Stop Loss can be one.");
        errors.AddRange(StopLoss.Validate().Select(x => $"Stop Loss: {x}"));
        return errors;
    }
}

/// <summary>
/// A step of an entry condition: what is met at the close of a candle. SuccessiveCandles is met at the close of the candle
/// that makes a run of Length candles of one colour, once a run, so a longer run does not meet it again.
/// </summary>
public sealed class StrategyEntry
{
    public const string SuccessiveCandles = nameof(SuccessiveCandles);
    public static readonly string[] Types = [SuccessiveCandles];

    public string Type { get; set; } = SuccessiveCandles;

    public EnumCandleColour Colour { get; set; } = EnumCandleColour.Green;

    public int Length { get; set; } = 4;

    /// <summary>
    /// The most candles from the candle that met the step before to the one that meets this; no limit when not set.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Within { get; set; }

    /// <summary>
    /// How many candles before the candle that meets the step it reads: a run's candles before its Length-th.
    /// </summary>
    [JsonIgnore]
    public int Reads => Type == SuccessiveCandles ? Math.Max(0, Length - 1) : 0;

    /// <summary>
    /// What is wrong with the entry, as messages; empty when it can be run.
    /// </summary>
    public List<string> Validate()
    {
        if (!Types.Contains(Type))
            return [$"\"{Type}\" is not an entry type: {string.Join(", ", Types)}."];
        var errors = new List<string>();
        if (Length < 1)
            errors.Add("Length must be at least 1.");
        if (Within < 1)
            errors.Add("Within must be at least 1 candle.");
        return errors;
    }
}

/// <summary>
/// Where a take profit or stop loss is. Some types are a distance from the entry: AverageCandle is Percent of the average
/// size of the last Candles candles up to the entry candle, measured by Measure; Percent is Percent of the entry price.
/// Others are a price level: HighestWick is the highest high of the last Candles candles up to the entry candle, and
/// LowestWick the lowest low. RiskRatio, for a stop loss only, makes the take profit a target of Ratio R: the stop loss is
/// the reward (the take profit's distance from the entry) divided by Ratio, so the risk, 1R, is that far.
/// </summary>
public sealed class StrategyTarget
{
    public const string AverageCandle = nameof(AverageCandle);
    public const string Percent = nameof(Percent);
    public const string HighestWick = nameof(HighestWick);
    public const string LowestWick = nameof(LowestWick);
    public const string RiskRatio = nameof(RiskRatio);
    public static readonly string[] Types = [AverageCandle, Percent, HighestWick, LowestWick, RiskRatio];

    /// <summary>
    /// Whether the target is a price level of its own rather than a distance from the entry.
    /// </summary>
    [JsonIgnore]
    public bool IsLevel => Type is HighestWick or LowestWick;

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
    /// set from the take profit's price, which takeProfit gives.
    /// </summary>
    public double? Price(IReadOnlyList<Price> prices, int index, EnumTradeDirection direction, bool profit, double? takeProfit = null)
    {
        var entry = prices[index].Close;
        var side = (direction == EnumTradeDirection.Long) == profit ? 1 : -1;
        if (Type == RiskRatio)
            return takeProfit is double reward && reward != entry ? entry + side * Math.Abs(reward - entry) / Ratio : null;
        if (IsLevel)
            return Level(prices, index) is double level && (level - entry) * side > 0 ? level : null;
        return Distance(prices, index) is double distance && distance > 0 ? entry + side * distance : null;
    }

    /// <summary>
    /// The price level a HighestWick or LowestWick target is at for a trade entered at the close of candle index; null for
    /// the other types, or when there are not enough candles before it.
    /// </summary>
    public double? Level(IReadOnlyList<Price> prices, int index)
    {
        if (!IsLevel || index - Candles + 1 < 0)
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
/// The strategies kept in strategies.json.
/// </summary>
public sealed class StrategyBook
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public List<Strategy> Strategies { get; set; } = [];

    /// <summary>
    /// Reads strategies.json's text.
    /// </summary>
    public static StrategyBook Parse(string json) =>
        JsonSerializer.Deserialize<StrategyBook>(json, JsonOptions) ?? new StrategyBook();
}
