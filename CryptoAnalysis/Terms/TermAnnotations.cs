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
    PricePoint? MsbReference);

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
    /// Runs the swing detectors over the prices and returns every structure point and break they find.
    /// </summary>
    public static List<TermAnnotation> Annotate(List<Price> prices, EnumCloseType closeType)
    {
        var upswings = prices.ToUpswings(closeType, trimStart: true);
        var downswings = prices.ToDownswings(closeType, trimStart: true);

        var annotations = new List<TermAnnotation>();

        foreach (var upswing in upswings)
        {
            Add(annotations, EnumAnnotationType.HigherHigh, EnumSwingDirection.Up, upswing.InitialPrice, closeType);
            Add(annotations, EnumAnnotationType.HigherLow, EnumSwingDirection.Up, upswing.SwingLow(closeType), closeType);
            Add(annotations, EnumAnnotationType.BreakOfStructure, EnumSwingDirection.Up, upswing.BreakOfStructure, closeType);
            Add(annotations, EnumAnnotationType.MarketStructureBreak, EnumSwingDirection.Up, upswing.MarketStructureBreak, closeType);
        }

        foreach (var downswing in downswings)
        {
            Add(annotations, EnumAnnotationType.LowerLow, EnumSwingDirection.Down, downswing.SwingLow(closeType), closeType);
            Add(annotations, EnumAnnotationType.LowerHigh, EnumSwingDirection.Down, downswing.SwingHigh(closeType), closeType);
            Add(annotations, EnumAnnotationType.BreakOfStructure, EnumSwingDirection.Down, downswing.BreakOfStructure, closeType);
            Add(annotations, EnumAnnotationType.MarketStructureBreak, EnumSwingDirection.Down, downswing.MarketStructureBreak, closeType);
        }

        return annotations
            .Distinct()
            .OrderBy(x => x.Time)
            .ThenBy(x => x.Type)
            .ThenBy(x => x.Direction)
            .ToList();
    }

    /// <summary>
    /// Runs the swing detectors over the prices and returns the outline of every upswing and downswing, ordered by start time.
    /// </summary>
    public static List<SwingOutline> Swings(List<Price> prices, EnumCloseType closeType)
    {
        var upswings = prices.ToUpswings(closeType, trimStart: true).Select(swing => Outline(
            EnumSwingDirection.Up, swing, swing.SwingLow(closeType), swing.PreviousUpswing?.SwingLow(closeType), closeType));

        var downswings = prices.ToDownswings(closeType, trimStart: true).Select(swing => Outline(
            EnumSwingDirection.Down, swing, swing.SwingHigh(closeType), swing.PreviousDownswing?.SwingHigh(closeType), closeType));

        return upswings.Concat(downswings).OrderBy(x => x.Start.Time).ThenBy(x => x.Direction).ToList();
    }

    private static SwingOutline Outline(EnumSwingDirection direction, Swing swing, Price? extreme, Price? msbReference, EnumCloseType closeType)
    {
        PricePoint? Point(Price? price) => price == null ? null : new PricePoint(price.DateTime, price.CloseValue(closeType));

        var start = swing.InitialPrice;
        var end = swing.NextPrice ?? swing.Prices.Last();

        return new SwingOutline(
            direction,
            Point(start)!,
            Point(end)!,
            Point(extreme ?? start)!,
            Point(swing.BreakOfStructure),
            Point(swing.MarketStructureBreak),
            swing.MarketStructureBreak == null ? null : Point(msbReference));
    }

    private static void Add(List<TermAnnotation> annotations, EnumAnnotationType type, EnumSwingDirection direction, Price? price, EnumCloseType closeType)
    {
        if (price == null)
            return;

        annotations.Add(new TermAnnotation(type, direction, price.DateTime, price.CloseValue(closeType)));
    }
}
