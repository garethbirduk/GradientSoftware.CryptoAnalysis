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
/// </summary>
public sealed class Strategy
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public EnumTradeDirection Direction { get; set; } = EnumTradeDirection.Long;

    public StrategyEntry Entry { get; set; } = new();

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
        errors.AddRange(Entry.Validate().Select(x => $"Entry: {x}"));
        errors.AddRange(TakeProfit.Validate().Select(x => $"Take Profit: {x}"));
        errors.AddRange(StopLoss.Validate().Select(x => $"Stop Loss: {x}"));
        return errors;
    }
}

/// <summary>
/// When a trade is entered. SuccessiveCandles enters at the close of the candle that makes a run of Length candles of one
/// colour, once a run, so a longer run does not enter again.
/// </summary>
public sealed class StrategyEntry
{
    public const string SuccessiveCandles = nameof(SuccessiveCandles);
    public static readonly string[] Types = [SuccessiveCandles];

    public string Type { get; set; } = SuccessiveCandles;

    public EnumCandleColour Colour { get; set; } = EnumCandleColour.Green;

    public int Length { get; set; } = 4;

    /// <summary>
    /// What is wrong with the entry, as messages; empty when it can be run.
    /// </summary>
    public List<string> Validate()
    {
        if (!Types.Contains(Type))
            return [$"\"{Type}\" is not an entry type: {string.Join(", ", Types)}."];
        return Length < 1 ? ["Length must be at least 1."] : [];
    }
}

/// <summary>
/// How far from the entry a take profit or stop loss is. AverageCandle is Percent of the average size of the last Candles
/// candles up to the entry candle, measured by Measure; Percent is Percent of the entry price.
/// </summary>
public sealed class StrategyTarget
{
    public const string AverageCandle = nameof(AverageCandle);
    public const string Percent = nameof(Percent);
    public static readonly string[] Types = [AverageCandle, Percent];

    public string Type { get; set; } = AverageCandle;

    [JsonPropertyName("percent")]
    public double Percentage { get; set; } = 100;

    public int Candles { get; set; } = 4;

    public EnumCandleMeasure Measure { get; set; } = EnumCandleMeasure.Body;

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
        if (Type == AverageCandle && Candles < 1)
            errors.Add("Candles must be at least 1.");
        return errors;
    }

    /// <summary>
    /// The distance from the entry price to the target for a trade entered at the close of candle index; null when there
    /// are not enough candles before it to measure.
    /// </summary>
    public double? Distance(IReadOnlyList<Price> prices, int index)
    {
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
