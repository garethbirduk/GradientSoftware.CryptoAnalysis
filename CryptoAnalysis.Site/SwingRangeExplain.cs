using System.Text.Json.Nodes;

namespace Gradient.CryptoAnalysis.Site;

/// <summary>
/// The Range found by Swings (see <see cref="SwingRanges"/>) as a thing an analysis tells of: the leg it lies inside,
/// its two ends, the Swings one order finer that were counted, how it holds and how it ended.
/// </summary>
public static partial class Explain
{
    // The blocks a Range by Swings is told in, by name, in the order they are told.
    private static readonly (string Name, Func<SwingRangeRead, Block> Tell)[] SwingRangeBlocks =
        [("Leg", SwingRangeLeg), ("Found", SwingRangeFound), ("Holds", SwingRangeHolds), ("Weak", SwingRangeWeak), ("Ends", SwingRangeEnds)];

    // A Range by Swings as known at a candle, read once for its blocks: its two ends by candle and price, and where it
    // ended, when it has by Until.
    private sealed record SwingRangeRead(List<Price> Prices, SwingRange Range, int Until, int Count)
    {
        public bool LegIsHigh => Range.LegKind == EnumPivotKind.High;
        public double High => LegIsHigh ? Prices[Range.Leg].Close : Prices[Range.Other].Close;
        public double Low => LegIsHigh ? Prices[Range.Other].Close : Prices[Range.Leg].Close;
        public int? EndedAt => Range.End is int end && end <= Until ? end : null;

        // The Swings counted move from the leg's own end back toward the end it came from.
        public string Swing => SwingName(LegIsHigh ? EnumSwingDirection.Down : EnumSwingDirection.Up);
        public TimeSpan Period => Length(Prices);
    }

    // The store of these prices: the one held for the series (see StructureStores), or else read from them.
    private static StructureStore StoreOf(List<Price> prices) => StructureStores.Of(prices) ?? StructureStore.Read(prices, Basis);

    // The Range by Swings at a level that a candle is in, as known at a cursor: the one identified on the candle, or else
    // the last identified of those that hold there. Null when there is none.
    private static SwingRange? SwingRangeAt(List<Price> prices, int index, int level, int cursor, int swings)
    {
        var known = Math.Clamp(cursor, index, prices.Count - 1);
        var found = SwingRanges.Over(StoreOf(prices), swings, index, index, known).Where(x => x.Level == level).ToList();
        return found.FirstOrDefault(x => x.Identified == index) ?? found.LastOrDefault();
    }

    // The chapter of a Range by Swings: from the end its leg came from to the close that ended it, or to the cursor while it
    // holds, with the layers that draw its leg and its box.
    private static JsonObject? SwingRangeChapter(List<Price> prices, int index, int level, int cursor, bool inPlace, ExplainOptions options, out int? keeps)
    {
        keeps = null;
        if (SwingRangeAt(prices, index, level, cursor, options.RangeSwings) is not { } range)
            return null;
        var (first, last) = (range.Other, range.End ?? cursor);
        var section = new JsonObject
        {
            ["chapter"] = $"{Order(level)} Range by Swings", ["explain"] = "SwingRange", ["at"] = index, ["level"] = level, ["from"] = first, ["until"] = last,
            ["view"] = new JsonArray(Math.Max(0, first - Margin), Math.Min(prices.Count - 1, last + Margin)), ["speed"] = Pace(last - first),
            ["layers"] = Layers($"level{level}", "close", "sawtooth", "SwingRange"), ["runs"] = options.ToJson(),
        };
        if (inPlace)
        {
            section["known"] = cursor;
            keeps = first;
        }

        return section;
    }

    /// <summary>
    /// The written parts of a section that explains the Range by Swings a candle is in at a level, as known at the cursor
    /// (known, else the last candle): the leg and its two ends, the Swings one order finer that found it, how it holds, how
    /// many of those Swings are Weak, and how it ended. swings is the count of Swings a Range is found by. Null, with the
    /// problem, when no such Range has the candle.
    /// </summary>
    public static JsonObject? SwingRange(List<Price> prices, int at, int level, int reached, out string problem, int? runsTo = null, int? known = null, IReadOnlyCollection<string>? blocks = null, int swings = SwingRanges.DefaultSwings)
    {
        var cursor = known ?? prices.Count - 1;
        if (SwingRangeAt(prices, at, level, cursor, swings) is not { } range)
        {
            problem = $"no Range by Swings at level {level} has #{at} in it";
            return null;
        }

        problem = "";
        var until = Math.Min(runsTo ?? range.End ?? cursor, prices.Count - 1);
        var r = new SwingRangeRead(prices, range, until, swings);
        return Section([Math.Max(0, range.Other - Margin), Math.Min(prices.Count - 1, until + Margin)], until > reached ? until : null,
            SwingRangeBlocks.Where(x => blocks == null || blocks.Contains(x.Name)).Select(x => x.Tell(r) with { Name = x.Name }).ToArray());
    }

    // What a Range by Swings is; then this one's leg, from the end it came from to its own end.
    private static Block SwingRangeLeg(SwingRangeRead r)
    {
        var g = r.Range;
        var (from, to) = (r.Prices[g.Other], r.Prices[g.Leg]);
        return new Block(
        [
            Define(g.Other, "SwingRange.what"),
            Say(g.Leg, $"A {Order(g.Level)} leg {(r.LegIsHigh ? "up" : "down")} from {Money(from.Close)}, {At(from.DateTime, r.Period)}, to {Money(to.Close)}, {At(to.DateTime, r.Period)}."),
            Pin(g.Other, "close", r.LegIsHigh ? "below" : "above", $"the leg begins: {Money(from.Close)}", 4),
            Pin(g.Leg, "close", r.LegIsHigh ? "above" : "below", $"the leg ends: {Money(to.Close)}", 4),
        ], [$"level{g.Level}", "sawtooth"]);
    }

    // The count that found it: where, and its box.
    private static Block SwingRangeFound(SwingRangeRead r)
    {
        var g = r.Range;
        return new Block(
        [
            Define(g.Identified, "SwingRange.box"),
            Say(g.Identified, $"{Capital(Words(r.Count))} {Order(g.Inner)} {r.Swing}s are Confirmed inside the leg by {Clock(r.Prices[g.Identified].DateTime, r.Period)} on {r.Prices[g.Identified].DateTime:d MMMM}, with the price still between its ends: a {Order(g.Level)} Range by Swings, from {Money(r.Low)} to {Money(r.High)}."),
            Pin(g.Identified, "close", r.LegIsHigh ? "below" : "above", "Range found", 4),
        ], ["SwingRange"]);
    }

    // How it holds, and how long this one has.
    private static Block SwingRangeHolds(SwingRangeRead r)
    {
        var g = r.Range;
        var held = (r.EndedAt ?? r.Until) - g.Identified;
        return new Block(
        [
            Define(r.Until, "SwingRange.holds"),
            Say(r.Until, $"It {(r.EndedAt != null ? "held" : "has held")} for {held} candles, every close between {Money(r.Low)} and {Money(r.High)}."),
        ], []);
    }

    // The Swings inside it by then, and how many of them are Weak.
    private static Block SwingRangeWeak(SwingRangeRead r)
    {
        var g = r.Range;
        return new Block(
        [
            Define(r.Until, "SwingRange.weak"),
            Say(r.Until, $"{Capital(Words(g.Swings))} {Order(g.Inner)} {r.Swing}s inside it{(r.EndedAt != null ? "" : " so far")}: {(g.Weak == 0 ? "none Weak" : $"{Words(g.Weak)} Weak")}."),
        ], []);
    }

    // Its end: the close beyond one of its ends, or that it still holds.
    private static Block SwingRangeEnds(SwingRangeRead r)
    {
        if (r.EndedAt is not int at)
            return new Block([Say(r.Until, "Still holding: no close beyond either end.")], []);
        var close = r.Prices[at].Close;
        var above = close >= r.High;
        return new Block(
        [
            Say(at, $"Ended {At(r.Prices[at].DateTime, r.Period)}: a close of {Money(close)}, {(above ? $"above its high of {Money(r.High)}" : $"below its low of {Money(r.Low)}")}."),
            Pin(at, "close", above ? "above" : "below", "Range ends", 4),
        ], []);
    }
}
