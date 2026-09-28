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
/// The geometry of one detected swing, enough to draw it and the levels its breaks are measured against.
/// Start is the swing's first price (the HH or LL); End is the break candle, or the last price if the swing never broke.
/// A swing is only confirmed once it has a break of structure; until then its extreme (HL or LH) is not final.
/// Start's price is also the level the break of structure crosses.
/// MsbReference is the previous swing's protective point (its HL or LH), which the market structure break crosses.
/// </summary>
public sealed record SwingOutline(
    EnumSwingDirection Direction,
    PricePoint Start,
    PricePoint End,
    PricePoint Extreme,
    PricePoint? BreakOfStructure,
    PricePoint? MarketStructureBreak,
    PricePoint? MsbReference)
{
    public bool Confirmed => BreakOfStructure != null;
}

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
    /// Returns the structure points (HH, HL, LH, LL) and breaks of structure from the sawtooth at the given level,
    /// plus market structure breaks from the old swing detectors. An upswing's MSB is a bearish break, a downswing's a bullish one.
    /// </summary>
    public static List<TermAnnotation> Annotate(List<Price> prices, EnumCloseType closeType, int level = 1)
    {
        var annotations = StructurePoints(prices, closeType, level);

        annotations.AddRange(Swings(prices, closeType, level)
            .Select(x => new TermAnnotation(
                x.Direction == EnumSwingDirection.Up ? EnumAnnotationType.BullishBreakOfStructure : EnumAnnotationType.BearishBreakOfStructure,
                x.Direction, x.BreakOfStructure!.Time, x.BreakOfStructure.Price)));

        annotations.AddRange(MarketStructureBreaks(prices, closeType)
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
    /// Returns each market structure break with the level it broke, from the old swing detectors: a bearish break closes below
    /// the previous upswing's low, a bullish break above the previous downswing's high.
    /// </summary>
    public static List<MarketStructureBreakOutline> MarketStructureBreaks(List<Price> prices, EnumCloseType closeType)
    {
        PricePoint Point(Price price) => new(price.DateTime, price.CloseValue(closeType));
        var breaks = new List<MarketStructureBreakOutline>();

        foreach (var upswing in prices.ToUpswings(closeType, trimStart: true))
        {
            if (upswing.MarketStructureBreak is Price broken && upswing.PreviousUpswing?.SwingLow(closeType) is Price reference)
                breaks.Add(new(EnumAnnotationType.BearishMarketStructureBreak, Point(reference), Point(broken)));
        }

        foreach (var downswing in prices.ToDownswings(closeType, trimStart: true))
        {
            if (downswing.MarketStructureBreak is Price broken && downswing.PreviousDownswing?.SwingHigh(closeType) is Price reference)
                breaks.Add(new(EnumAnnotationType.BullishMarketStructureBreak, Point(reference), Point(broken)));
        }

        return breaks.OrderBy(x => x.Break.Time).ToList();
    }

    /// <summary>
    /// Returns the confirmed swings at a sawtooth level, ordered by start time.
    /// </summary>
    public static List<SwingOutline> Swings(List<Price> prices, EnumCloseType closeType, int level)
    {
        var basis = closeType == EnumCloseType.Close ? EnumPriceBasis.Close : EnumPriceBasis.Wick;
        var levels = Sawtooth.Levels(prices, basis, level);
        return Sawtooth.Swings(prices, levels, basis, level).OrderBy(x => x.Start.Time).ToList();
    }

    /// <summary>
    /// Classifies each pivot of a sawtooth level against the previous pivot of the same kind:
    /// a high above the previous high is HH, below it LH; a low above the previous low is HL, below it LL.
    /// The first high and first low have nothing to compare with, and equal pivots are neither, so those are left out.
    /// A pivot on the last candle is also left out: its run has not ended, so it is not yet a formed high or low.
    /// </summary>
    public static List<TermAnnotation> StructurePoints(List<Price> prices, EnumCloseType closeType, int level)
    {
        var basis = closeType == EnumCloseType.Close ? EnumPriceBasis.Close : EnumPriceBasis.Wick;
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
