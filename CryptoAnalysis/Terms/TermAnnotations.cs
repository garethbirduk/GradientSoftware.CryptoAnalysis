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
    /// sawtooth at the given level, plus candle runs. A swing is asserted at its start. An upswing's MSB is a bearish break,
    /// a downswing's a bullish one.
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

        annotations.AddRange(MarketStructureBreaks(prices, closeType, level)
            .Select(x => new TermAnnotation(x.Type,
                x.Type == EnumAnnotationType.BullishMarketStructureBreak ? EnumSwingDirection.Up : EnumSwingDirection.Down,
                x.Break.Time, x.Break.Price)));

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
    /// Classifies each pivot of a sawtooth level against the previous pivot of the same kind:
    /// a high above the previous high is HH, below it LH; a low above the previous low is HL, below it LL.
    /// The first high and first low have nothing to compare with, and equal pivots are neither, so those are left out.
    /// A pivot on the last candle is also left out: its run has not ended, so it is not yet a formed high or low.
    /// </summary>
    public static List<TermAnnotation> StructurePoints(List<Price> prices, EnumCloseType closeType, int level)
    {
        var basis = Basis(closeType);
        var levels = Sawtooth.Levels(prices, basis, level);
        var pivots = levels.Count == 0 ? [] : levels[Math.Min(level, levels.Count - 1)].Pivots;

        var points = new List<TermAnnotation>();
        double? lastHigh = null, lastLow = null;
        foreach (var pivot in pivots.Where(p => p.Index != prices.Count - 1))
        {
            if (pivot.Kind == EnumPivotKind.High)
            {
                if (lastHigh is double previous && pivot.Price != previous)
                    points.Add(pivot.Price > previous
                        ? new TermAnnotation(EnumAnnotationType.HigherHigh, EnumSwingDirection.Up, pivot.Time, pivot.Price)
                        : new TermAnnotation(EnumAnnotationType.LowerHigh, EnumSwingDirection.Down, pivot.Time, pivot.Price));
                lastHigh = pivot.Price;
            }
            else if (pivot.Kind == EnumPivotKind.Low)
            {
                if (lastLow is double previous && pivot.Price != previous)
                    points.Add(pivot.Price > previous
                        ? new TermAnnotation(EnumAnnotationType.HigherLow, EnumSwingDirection.Up, pivot.Time, pivot.Price)
                        : new TermAnnotation(EnumAnnotationType.LowerLow, EnumSwingDirection.Down, pivot.Time, pivot.Price));
                lastLow = pivot.Price;
            }
        }

        return points;
    }

    private static void Add(List<TermAnnotation> annotations, EnumAnnotationType type, EnumSwingDirection direction, Price? price, EnumCloseType closeType)
    {
        if (price == null)
            return;

        annotations.Add(new TermAnnotation(type, direction, price.DateTime, price.CloseValue(closeType)));
    }
}
