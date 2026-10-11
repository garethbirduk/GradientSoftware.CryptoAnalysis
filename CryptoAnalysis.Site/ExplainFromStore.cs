namespace Gradient.CryptoAnalysis.Site;

public static partial class Explain
{
    /// <summary>
    /// What is at a candle, as <see cref="At(List{Price}, string, string?, ExplainOptions?, int)"/> lists it, looked up in
    /// the prices' structure store and not worked out from every candle: the pivots and Swings as known at the cursor come
    /// from the store, and only the leg still running there is read from the candles.
    /// </summary>
    public static List<Thing> At(List<Price> prices, string time, string? seen, StructureStore store, ExplainOptions? options = null, int maxLevel = 8)
    {
        options ??= ExplainOptions.Default;
        var index = Tours.AnchorOf(time, prices);
        var cursor = seen == null ? prices.Count - 1 : Math.Max(index, Tours.AnchorOf(seen, prices));
        var known = cursor < prices.Count - 1 ? prices.GetRange(0, cursor + 1) : prices;
        var things = new List<Thing> { new("Candle", null, "Candle") };
        if (RunAt(known, index, options) is { } run)
            things.Add(new Thing("SuccessiveCandles", null, $"{Capital(Words(run.Length))} Successive {(known[index].Close > known[index].Open ? "Green" : "Red")} Candles"));

        // The levels the page reads: one past maxLevel, as the levels of the prices are read to there.
        var pivots = store.Pivots(0, cursor, cursor, maxLevel + 1);
        var swings = store.Swings(0, cursor, cursor, maxLevel + 1).ToLookup(x => x.Level);
        var deepest = pivots.Count > 0 ? pivots.Max(x => x.Level) : 0;

        // The highest high is on the cursor's candle when no candle before closed as high: a high still running, which the
        // store does not have, and the levels then end at it and not at the current price.
        var high = 0;
        for (var i = 1; i <= cursor; i++)
        {
            if (prices[i].Close > prices[high].Close)
                high = i;
        }

        var highRunning = high == cursor && cursor > 0;
        var when = prices[index].DateTime;
        for (var level = deepest; level >= 1; level--)
        {
            if (LabelAt(pivots, level, index, cursor, highRunning, prices) is { } label)
                things.Add(new Thing("Point", level, $"{Order(level)} {label}"));

            var ofLevel = swings[level].Select(x => Outline(x, prices)).OrderBy(x => x.Start.Time).ToList();
            if (ofLevel.LastOrDefault(x => x.Start.Time <= when && x.BreakOfStructure!.Time >= when) is { } swing)
                things.Add(new Thing("Swing", level, $"{Order(level)} {SwingName(swing.Direction)}"));
            // A Trend's start and end are its Swings' alone; the market structure breaks only count against it.
            if (Sawtooth.Trends(ofLevel, []).LastOrDefault(x => x.Start.Time <= when && (x.End == null || x.End.Time >= when)) is { } trend)
                things.Add(new Thing("Trend", level, $"{Order(level)} {TrendName(trend.Direction)}"));
        }

        return things;
    }

    // A stored Swing as the sawtooth gives one, its prices read from its candles: their closes, as the store is of closes.
    private static SwingOutline Outline(StoredSwing swing, List<Price> prices)
    {
        PricePoint Point(int candle) => new(prices[candle].DateTime, prices[candle].Close);
        var broke = Point(swing.Break);
        return new SwingOutline(swing.Level, swing.Direction, Point(swing.Start), broke, Point(swing.Extreme), broke);
    }

    // The label a candle has at a level as known at the cursor (HH, LH, HL or LL), as Sawtooth.Points gives it: the candle
    // is one of the level's pivots, which are those of this level and every one above, or it is the furthest point so far
    // of the leg still running from the last of them, or the highest high still running; and its price is against the one
    // before it of its kind.
    private static string? LabelAt(List<StoredPivot> pivots, int level, int index, int cursor, bool highRunning, List<Price> prices)
    {
        // The level's pivots are in candle order; the last of them at or before the candle.
        var at = -1;
        var last = -1;
        for (var i = pivots.Count - 1; i >= 0 && (last < 0 || at < 0); i--)
        {
            if (pivots[i].Level > level)
                continue;
            if (last < 0)
                last = i;
            if (pivots[i].Candle <= index)
                at = i;
        }

        EnumPivotKind kind;
        double price;
        if (at >= 0 && pivots[at].Candle == index)
        {
            (kind, price) = (pivots[at].Kind, pivots[at].Price);
            at--;
        }
        else if (highRunning && index == cursor)
        {
            (kind, price) = (EnumPivotKind.High, prices[index].Close);
        }
        else if (!highRunning && last >= 0 && index > pivots[last].Candle)
        {
            // The leg still running from the level's last pivot: its furthest point so far, the first candle to reach it.
            var up = pivots[last].Kind == EnumPivotKind.Low;
            var best = pivots[last].Candle + 1;
            for (var i = best + 1; i <= cursor; i++)
            {
                if (up ? prices[i].Close > prices[best].Close : prices[i].Close < prices[best].Close)
                    best = i;
            }

            if (best != index)
                return null;
            (kind, price) = (up ? EnumPivotKind.High : EnumPivotKind.Low, prices[index].Close);
        }
        else
        {
            return null;
        }

        // The one before it of its kind, at this level.
        for (var i = at; i >= 0; i--)
        {
            if (pivots[i].Level > level || pivots[i].Kind != kind)
                continue;
            var high = kind == EnumPivotKind.High;
            return price == pivots[i].Price ? null : price > pivots[i].Price ? high ? "HH" : "HL" : high ? "LH" : "LL";
        }

        return null;
    }
}
