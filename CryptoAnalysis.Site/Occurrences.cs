using System.Text.Json.Nodes;

namespace Gradient.CryptoAnalysis.Site;

/// <summary>
/// Where each kind of thing a chart shows occurs in a whole dataset, as known at its last candle, for the page's List: a
/// row for each, with its level, where it begins and ends, and the candle and thing an analysis of it opens at.
/// </summary>
public static class Occurrences
{
    /// <summary>
    /// The kinds that can be listed, named as the page's layers are.
    /// </summary>
    public static readonly IReadOnlyList<string> Kinds =
    [
        "SwingRange", "Range", "Uptrend", "Downtrend", "Upswing", "Downswing", "BullishBreakOfStructure", "BearishBreakOfStructure",
        "BullishMarketStructureBreak", "BearishMarketStructureBreak",
    ];

    /// <summary>
    /// Every occurrence of a kind at levels up to maxLevel, in the order they begin; null for a kind that is not listed.
    /// The Ranges found by Swings are those of a count of Swings.
    /// </summary>
    public static JsonArray? Of(StructureStore store, string kind, int maxLevel, int rangeSwings = SwingRanges.DefaultSwings)
    {
        if (!Kinds.Contains(kind) || store.Candles == 0)
            return Kinds.Contains(kind) ? [] : null;

        var prices = store.Prices;
        var last = store.Candles - 1;
        string Time(int candle) => prices[candle].DateTime.ToString("yyyy-MM-ddTHH:mm");
        JsonObject Row(int level, EnumSwingDirection direction, int start, int? end, int at, string thing) => new()
        {
            ["index"] = start,
            ["start"] = Time(start),
            ["level"] = level,
            ["direction"] = direction.ToString(),
            ["end"] = end is int e ? Time(e) : null,
            // Where it runs to: its end, or the last candle while it goes on.
            ["until"] = Time(end ?? last),
            ["candles"] = (end ?? last) - start,
            ["at"] = Time(at),
            ["thing"] = thing,
        };

        var up = kind is "Uptrend" or "Upswing" or "BullishBreakOfStructure" or "BullishMarketStructureBreak" ? EnumSwingDirection.Up : EnumSwingDirection.Down;
        IEnumerable<JsonObject> rows = kind switch
        {
            "SwingRange" => SwingRanges.Over(store, rangeSwings, 0, last, last).Where(x => x.Level <= maxLevel).Select(x =>
            {
                var high = x.LegKind == EnumPivotKind.High;
                var row = Row(x.Level, high ? EnumSwingDirection.Up : EnumSwingDirection.Down, x.Identified, x.End, x.Identified, $"SwingRange:{x.Level}");
                var (low, top) = high ? (prices[x.Other].Close, prices[x.Leg].Close) : (prices[x.Leg].Close, prices[x.Other].Close);
                (row["low"], row["high"], row["height"], row["swings"], row["weak"]) = (low, top, 100 * (top - low) / low, x.Swings, x.Weak);
                return row;
            }),
            "Range" => store.Ranges(0, last, last, maxLevel).Select(x =>
            {
                var row = Row(x.Level, x.Direction, x.Identified, x.End, x.Identified, $"Swing:{x.Level}");
                var (low, top) = (prices[x.Low].Close, prices[x.High].Close);
                (row["low"], row["high"], row["height"], row["retracement"]) = (low, top, 100 * (top - low) / low, x.Retracement);
                return row;
            }),
            "Uptrend" or "Downtrend" => store.Trends(0, last, last, maxLevel).Where(x => x.Direction == up && x.Parts.Count > 0).Select(x =>
            {
                var row = Row(x.Level, x.Direction, x.Start, x.End, x.Parts[^1].Break, $"Trend:{x.Level}");
                (row["swings"], row["strength"]) = (x.Swings, 100 * x.Parts.Count(p => p.Strong) / x.Parts.Count);
                return row;
            }),
            "BullishMarketStructureBreak" or "BearishMarketStructureBreak" => store.Breaks(0, last, last, maxLevel)
                .Where(x => x.Type == (up == EnumSwingDirection.Up ? EnumAnnotationType.BullishMarketStructureBreak : EnumAnnotationType.BearishMarketStructureBreak))
                .Select(x => Row(x.Level, up, x.Break, x.Break, x.Break, $"Swing:{x.Level}")),
            _ => store.Swings(0, last, last, maxLevel).Where(x => x.Direction == up).Select(x => Row(x.Level, x.Direction, x.Start, x.Break, x.Break, $"Swing:{x.Level}")),
        };

        return new JsonArray(rows.OrderBy(x => x["index"]!.GetValue<int>()).ThenBy(x => x["level"]!.GetValue<int>()).ToArray<JsonNode>());
    }
}
