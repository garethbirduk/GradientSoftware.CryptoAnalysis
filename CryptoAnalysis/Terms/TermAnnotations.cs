using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gradient.CryptoAnalysis;

public enum EnumSwingDirection
{
    Up,
    Down,
}

/// <summary>
/// One occurrence of a term at a point in time, e.g. an upward break of structure at 14:00 closing at 64,210.
/// </summary>
public sealed record TermAnnotation(EnumAnnotationType Type, EnumSwingDirection Direction, DateTime Time, double Price);

public sealed record PricePoint(DateTime Time, double Price);

/// <summary>
/// The geometry of one detected swing at a sawtooth level, enough to draw it and the levels its breaks are measured against.
/// Start is the swing's first price (the high an upswing pulls back from, the low a downswing bounces from); End is the break candle.
/// A swing is only confirmed once it has a break of structure; until then its extreme (the pullback low or bounce high) is not final.
/// Start's price is the level the break of structure crosses, and Extreme's price the protective level a market structure break crosses.
/// </summary>
public sealed record SwingOutline(
    int Level,
    EnumSwingDirection Direction,
    PricePoint Start,
    PricePoint End,
    PricePoint Extreme,
    PricePoint? BreakOfStructure)
{
    public bool Confirmed => BreakOfStructure != null;
}

/// <summary>
/// A swing still forming: its start (the high or low to break) and the furthest counter-move so far. It has no break of
/// structure yet, so it is not a swing; see <see cref="Sawtooth.Candidates"/>.
/// </summary>
public sealed record CandidateSwing(int Level, EnumSwingDirection Direction, PricePoint Start, PricePoint Extreme);

/// <summary>
/// One swing of a trend, by its break of structure. It is weak when a market structure break against the trend falls inside
/// it, between its start and its break of structure, and strong when none does.
/// </summary>
public sealed record TrendSwing(PricePoint BreakOfStructure, bool Strong);

/// <summary>
/// A trend at a sawtooth level: consecutive swings in one direction. Start is the first swing's start, Confirmed the break of
/// structure that made it a trend, and End the break of structure of the first swing the other way (null while ongoing).
/// Swings counts its swings and MarketStructureBreaks the market structure breaks against it. Parts lists its swings in order,
/// Strong counts the strong ones, and Strength is their share of the swings as a whole percentage, rounded down.
/// </summary>
public sealed record TrendOutline(
    int Level,
    EnumSwingDirection Direction,
    PricePoint Start,
    PricePoint Confirmed,
    PricePoint? End,
    int Swings,
    int MarketStructureBreaks,
    IReadOnlyList<TrendSwing> Parts)
{
    public int Strong => Parts.Count(x => x.Strong);

    public int Strength => Parts.Count == 0 ? 0 : (int)Math.Floor(100.0 * Strong / Parts.Count);

    /// <summary>
    /// Compares the trends by value, their swings included, so a replay can tell when a trend has changed.
    /// </summary>
    public bool Equals(TrendOutline? other)
    {
        return other is not null && Level == other.Level && Direction == other.Direction && Start == other.Start
            && Confirmed == other.Confirmed && End == other.End && Swings == other.Swings
            && MarketStructureBreaks == other.MarketStructureBreaks && Parts.SequenceEqual(other.Parts);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(Level, Direction, Start, Confirmed, End, Swings, MarketStructureBreaks, Parts.Count);
    }
}

/// <summary>
/// A swing and its interim swings: the swings one level finer that start inside it, each with its own interims.
/// </summary>
public sealed record SwingNode(SwingOutline Swing, IReadOnlyList<SwingNode> Interims);

/// <summary>
/// A market structure break and the level it broke: Reference is the protective high or low, Break the close beyond it.
/// </summary>
public sealed record MarketStructureBreakOutline(EnumAnnotationType Type, PricePoint Reference, PricePoint Break);

/// <summary>
/// Detects every structure term in a price series and flattens them into one time-ordered list.
/// This is the data both the example tests and the term library consume.
/// </summary>
public static class TermAnnotations
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Returns the structure points (HH, HL, LH, LL), swings, breaks of structure and market structure breaks from the
    /// sawtooth at the given level, plus trends and candle runs. A swing is asserted at its start and a trend at its
    /// confirming break of structure. An upswing's MSB is a bearish break, a downswing's a bullish one.
    /// </summary>
    public static List<TermAnnotation> Annotate(List<Price> prices, EnumCloseType closeType, int level = 1)
    {
        var annotations = StructurePoints(prices, closeType, level);
        var swings = Swings(prices, closeType, level);

        annotations.AddRange(swings
            .Select(x => new TermAnnotation(
                x.Direction == EnumSwingDirection.Up ? EnumAnnotationType.Upswing : EnumAnnotationType.Downswing,
                x.Direction, x.Start.Time, x.Start.Price)));

        annotations.AddRange(swings
            .Select(x => new TermAnnotation(
                x.Direction == EnumSwingDirection.Up ? EnumAnnotationType.BullishBreakOfStructure : EnumAnnotationType.BearishBreakOfStructure,
                x.Direction, x.BreakOfStructure!.Time, x.BreakOfStructure.Price)));

        var breaks = Sawtooth.MarketStructureBreaks(prices, swings, Basis(closeType));
        annotations.AddRange(breaks
            .Select(x => new TermAnnotation(x.Type,
                x.Type == EnumAnnotationType.BullishMarketStructureBreak ? EnumSwingDirection.Up : EnumSwingDirection.Down,
                x.Break.Time, x.Break.Price)));

        annotations.AddRange(Sawtooth.Trends(swings, breaks)
            .Select(x => new TermAnnotation(
                x.Direction == EnumSwingDirection.Up ? EnumAnnotationType.Uptrend : EnumAnnotationType.Downtrend,
                x.Direction, x.Confirmed.Time, x.Confirmed.Price)));

        annotations.AddRange(CandleRuns.Runs(prices)
            .Select(x => new TermAnnotation(
                x.Green ? EnumAnnotationType.SuccessiveGreenCandles : EnumAnnotationType.SuccessiveRedCandles,
                x.Green ? EnumSwingDirection.Up : EnumSwingDirection.Down, x.End.Time, x.End.Price)));

        return annotations
            .Distinct()
            .OrderBy(x => x.Time)
            .ThenBy(x => x.Type)
            .ThenBy(x => x.Direction)
            .ToList();
    }

    /// <summary>
    /// Returns each market structure break at a sawtooth level with the protective level it broke: a bearish break is the
    /// first price below an upswing's pullback low, a bullish break the first price above a downswing's bounce high.
    /// </summary>
    public static List<MarketStructureBreakOutline> MarketStructureBreaks(List<Price> prices, EnumCloseType closeType, int level)
    {
        var basis = Basis(closeType);
        return Sawtooth.MarketStructureBreaks(prices, Swings(prices, closeType, level), basis);
    }

    /// <summary>
    /// Returns the trends at a sawtooth level.
    /// </summary>
    public static List<TrendOutline> Trends(List<Price> prices, EnumCloseType closeType, int level)
    {
        var swings = Swings(prices, closeType, level);
        return Sawtooth.Trends(swings, Sawtooth.MarketStructureBreaks(prices, swings, Basis(closeType)));
    }

    /// <summary>
    /// Returns the confirmed swings at a sawtooth level, ordered by start time.
    /// </summary>
    public static List<SwingOutline> Swings(List<Price> prices, EnumCloseType closeType, int level)
    {
        var basis = Basis(closeType);
        var levels = Sawtooth.Levels(prices, basis, level);
        return Sawtooth.Swings(prices, levels, basis, level).OrderBy(x => x.Start.Time).ToList();
    }

    /// <summary>
    /// Returns the confirmed swings at a sawtooth level, each with its interim swings down to depth levels finer.
    /// </summary>
    public static List<SwingNode> SwingTree(List<Price> prices, EnumCloseType closeType, int level, int depth)
    {
        var basis = Basis(closeType);
        var levels = Sawtooth.Levels(prices, basis, level + depth);
        return Sawtooth.SwingTree(prices, levels, basis, level, depth);
    }

    private static EnumPriceBasis Basis(EnumCloseType closeType) => closeType == EnumCloseType.Close ? EnumPriceBasis.Close : EnumPriceBasis.Wick;

    /// <summary>
    /// Returns the structure points at a sawtooth level (see <see cref="Sawtooth.Points"/>): each high or low as currently
    /// known, against the previous one of its kind. A high above the previous high is HH, below it LH; a low above the previous
    /// low is HL, below it LL. The first high and first low have nothing to compare with, and equal ones are neither, so those
    /// are left out. The latest high or low counts as soon as price makes it, even while price may still extend it.
    /// </summary>
    public static List<TermAnnotation> StructurePoints(List<Price> prices, EnumCloseType closeType, int level)
    {
        var basis = Basis(closeType);
        var levels = Sawtooth.Levels(prices, basis, level);
        if (levels.Count == 0)
            return [];

        return Sawtooth.Points(prices, levels[Math.Min(level, levels.Count - 1)], basis)
            .Where(x => x.Type != null)
            .Select(x => new TermAnnotation(x.Type!.Value,
                x.Type is EnumAnnotationType.HigherHigh or EnumAnnotationType.HigherLow ? EnumSwingDirection.Up : EnumSwingDirection.Down,
                x.Time, x.Price))
            .ToList();
    }

    private static void Add(List<TermAnnotation> annotations, EnumAnnotationType type, EnumSwingDirection direction, Price? price, EnumCloseType closeType)
    {
        if (price == null)
            return;

        annotations.Add(new TermAnnotation(type, direction, price.DateTime, price.CloseValue(closeType)));
    }
}
