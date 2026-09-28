namespace Gradient.CryptoAnalysis;

/// <summary>
/// Which prices define highs and lows: closes only, or wicks (highs from High, lows from Low).
/// </summary>
public enum EnumPriceBasis
{
    Close,
    Wick,
}

public enum EnumPivotKind
{
    Start,
    Low,
    High,
    Current,
}

/// <summary>
/// A point on a sawtooth. Start is the first price, which is neither a high nor a low; Current is the last price,
/// which ends the final, incomplete leg. Index is the candle index.
/// </summary>
public sealed record SawtoothPivot(EnumPivotKind Kind, int Index, DateTime Time, double Price);

/// <summary>
/// One resolution of the sawtooth: the start, then alternating highs and lows, optionally ending at the current price.
/// </summary>
public sealed record SawtoothLevel(int Level, IReadOnlyList<SawtoothPivot> Pivots);

/// <summary>
/// Builds the multi-resolution sawtooth. Level 0 is two lines: first price to highest high to current price (one line when
/// the highest high is the current price). Each further level splits every leg at its pullbacks: inside an upleg, each run of
/// new highs ends at a high, and the lowest price before the next new high is the low (the floor moves up at each break of
/// structure); a dip before the first new high is the leg's first low.
/// Downlegs mirror this with runs of new lows. A new extreme must strictly beat the previous one, and ties go to the first.
/// </summary>
public static class Sawtooth
{
    /// <summary>
    /// Builds levels 0 to maxLevel, stopping early once a level adds no new pivots.
    /// </summary>
    public static List<SawtoothLevel> Levels(List<Price> prices, EnumPriceBasis basis, int maxLevel = 10)
    {
        if (prices.Count == 0)
            return [];

        var levels = new List<SawtoothLevel> { new(0, LevelZero(prices, basis)) };
        while (levels.Count <= maxLevel)
        {
            var next = Refine(prices, basis, levels[^1].Pivots);
            if (next.Count == levels[^1].Pivots.Count)
                break;
            levels.Add(new SawtoothLevel(levels.Count, next));
        }

        return levels;
    }

    /// <summary>
    /// Returns the confirmed swings at a level (1 or above). A swing lies inside one leg of the level above and runs in that
    /// leg's direction: in an upleg, a high, the low after it, and the break of structure (the first price beyond the high);
    /// in a downleg the mirror. A leg's last high or low that has not been broken is not yet a swing.
    /// </summary>
    public static List<SwingOutline> Swings(List<Price> prices, IReadOnlyList<SawtoothLevel> levels, EnumPriceBasis basis, int level)
    {
        var swings = new List<SwingOutline>();
        if (level < 1 || level >= levels.Count)
            return swings;

        var parent = levels[level - 1].Pivots;
        var child = levels[level].Pivots.Where(p => p.Kind is EnumPivotKind.High or EnumPivotKind.Low).ToList();
        var last = prices.Count - 1;

        for (var i = 0; i + 1 < parent.Count; i++)
        {
            var from = parent[i];
            var to = parent[i + 1];
            var end = to.Kind == EnumPivotKind.Current ? last : to.Index;
            var up = to.Kind == EnumPivotKind.High || (to.Kind == EnumPivotKind.Current && from.Kind == EnumPivotKind.Low);
            var extremeKind = up ? EnumPivotKind.High : EnumPivotKind.Low;

            var turns = child.Where(p => p.Index >= from.Index && p.Index <= end).ToList();
            for (var t = 0; t + 2 < turns.Count; t++)
            {
                var start = turns[t];
                var counter = turns[t + 1];
                var next = turns[t + 2];
                if (start.Kind != extremeKind)
                    continue;

                for (var c = counter.Index + 1; c <= next.Index; c++)
                {
                    var value = up ? HighOf(prices[c], basis) : LowOf(prices[c], basis);
                    if (up ? value > start.Price : value < start.Price)
                    {
                        var breakPoint = new PricePoint(prices[c].DateTime, value);
                        swings.Add(new SwingOutline(
                            level,
                            up ? EnumSwingDirection.Up : EnumSwingDirection.Down,
                            new PricePoint(start.Time, start.Price),
                            breakPoint,
                            new PricePoint(counter.Time, counter.Price),
                            breakPoint));
                        break;
                    }
                }
            }
        }

        return swings;
    }

    /// <summary>
    /// Returns the swings one level finer that start inside a swing, before its break of structure: in an upswing, the
    /// downswings of its pullback and the upswings of the climb back to the break; in a downswing the mirror.
    /// </summary>
    public static List<SwingOutline> Interims(SwingOutline swing, IEnumerable<SwingOutline> finer)
    {
        return finer
            .Where(x => x.Level == swing.Level + 1 && x.Start.Time >= swing.Start.Time && x.Start.Time < swing.End.Time)
            .OrderBy(x => x.Start.Time)
            .ToList();
    }

    /// <summary>
    /// Returns the confirmed swings at a level, each with its interim swings, recursively down to depth levels finer.
    /// </summary>
    public static List<SwingNode> SwingTree(List<Price> prices, IReadOnlyList<SawtoothLevel> levels, EnumPriceBasis basis, int level, int depth)
    {
        var byLevel = Enumerable.Range(level, depth + 1)
            .Select(l => Swings(prices, levels, basis, l).OrderBy(x => x.Start.Time).ToList())
            .ToList();

        List<SwingNode> Nodes(IEnumerable<SwingOutline> swings, int d) => swings
            .Select(x => new SwingNode(x, d < depth ? Nodes(Interims(x, byLevel[d + 1]), d + 1) : []))
            .ToList();

        return Nodes(byLevel[0], 0);
    }

    /// <summary>
    /// Returns the market structure breaks for a level's swings. From its break of structure on, each swing's extreme is its
    /// protective level: the first price beyond it (below an upswing's pullback low, above a downswing's bounce high) is a
    /// market structure break. The protective level lasts until the next swing in the same direction breaks structure and
    /// replaces it, and breaks at most once. A market structure break does not end the swing.
    /// </summary>
    public static List<MarketStructureBreakOutline> MarketStructureBreaks(List<Price> prices, IReadOnlyList<SwingOutline> swings, EnumPriceBasis basis)
    {
        var index = new Dictionary<DateTime, int>();
        for (var i = 0; i < prices.Count; i++)
            index.TryAdd(prices[i].DateTime, i);

        var breaks = new List<MarketStructureBreakOutline>();
        foreach (var direction in new[] { EnumSwingDirection.Up, EnumSwingDirection.Down })
        {
            var up = direction == EnumSwingDirection.Up;
            var confirmed = swings.Where(x => x.Direction == direction && x.BreakOfStructure != null)
                .OrderBy(x => x.BreakOfStructure!.Time)
                .ToList();

            for (var s = 0; s < confirmed.Count; s++)
            {
                var swing = confirmed[s];
                var from = index[swing.BreakOfStructure!.Time] + 1;
                var to = s + 1 < confirmed.Count ? index[confirmed[s + 1].BreakOfStructure!.Time] - 1 : prices.Count - 1;

                for (var c = from; c <= to; c++)
                {
                    var value = up ? LowOf(prices[c], basis) : HighOf(prices[c], basis);
                    if (up ? value < swing.Extreme.Price : value > swing.Extreme.Price)
                    {
                        breaks.Add(new MarketStructureBreakOutline(
                            up ? EnumAnnotationType.BearishMarketStructureBreak : EnumAnnotationType.BullishMarketStructureBreak,
                            swing.Extreme,
                            new PricePoint(prices[c].DateTime, value)));
                        break;
                    }
                }
            }
        }

        return breaks.OrderBy(x => x.Break.Time).ThenBy(x => x.Type).ToList();
    }

    private static double HighOf(Price price, EnumPriceBasis basis) => basis == EnumPriceBasis.Wick ? price.High : price.Close;

    private static double LowOf(Price price, EnumPriceBasis basis) => basis == EnumPriceBasis.Wick ? price.Low : price.Close;

    private static List<SawtoothPivot> LevelZero(List<Price> prices, EnumPriceBasis basis)
    {
        var last = prices.Count - 1;
        var ath = 0;
        for (var i = 1; i <= last; i++)
        {
            if (HighOf(prices[i], basis) > HighOf(prices[ath], basis))
                ath = i;
        }

        var pivots = new List<SawtoothPivot>();
        var athPivot = Pivot(EnumPivotKind.High, prices, ath, basis);

        // When the first candle is the high, the start and the high are the same point.
        if (ath != 0)
            pivots.Add(Pivot(EnumPivotKind.Start, prices, 0, basis));
        pivots.Add(athPivot);

        var current = Pivot(EnumPivotKind.Current, prices, last, basis);
        if (!(ath == last && current.Price == athPivot.Price))
            pivots.Add(current);

        return pivots;
    }

    private static List<SawtoothPivot> Refine(List<Price> prices, EnumPriceBasis basis, IReadOnlyList<SawtoothPivot> pivots)
    {
        var last = prices.Count - 1;
        var result = new List<SawtoothPivot> { pivots[0] };

        for (var i = 0; i + 1 < pivots.Count; i++)
        {
            var from = pivots[i];
            var to = pivots[i + 1];

            var interior = to.Kind switch
            {
                EnumPivotKind.High => Chain(prices, basis, from, to.Index, up: true, includeLastExtreme: false),
                EnumPivotKind.Low => Chain(prices, basis, from, to.Index, up: false, includeLastExtreme: false),
                _ => Chain(prices, basis, from, last, up: from.Kind == EnumPivotKind.Low, includeLastExtreme: true)
                    .Where(x => x.Index != last)
                    .ToList(),
            };

            result.AddRange(interior);
            result.Add(to);
        }

        return result;
    }

    /// <summary>
    /// Finds the alternating pivots inside a leg that starts at from and runs to candle end.
    /// For an upleg: the end of each run of new highs, and the first lowest price before the next new high.
    /// </summary>
    private static List<SawtoothPivot> Chain(List<Price> prices, EnumPriceBasis basis, SawtoothPivot from, int end, bool up, bool includeLastExtreme)
    {
        double Extreme(Price p) => up ? HighOf(p, basis) : LowOf(p, basis);
        double Counter(Price p) => up ? LowOf(p, basis) : HighOf(p, basis);
        bool Beats(double value, double best) => up ? value > best : value < best;

        var start = from.Index + 1;
        var best = from.Kind == EnumPivotKind.Start ? from.Price : Extreme(prices[from.Index]);

        var runEnds = new List<int>();
        var runStarts = new List<int>();
        var inRun = false;
        for (var i = start; i <= end; i++)
        {
            if (Beats(Extreme(prices[i]), best))
            {
                best = Extreme(prices[i]);
                if (!inRun)
                    runStarts.Add(i);
                if (inRun)
                    runEnds[^1] = i;
                else
                    runEnds.Add(i);
                inRun = true;
            }
            else
            {
                inRun = false;
            }
        }

        var extremeKind = up ? EnumPivotKind.High : EnumPivotKind.Low;
        var counterKind = up ? EnumPivotKind.Low : EnumPivotKind.High;
        var chain = new List<SawtoothPivot>();

        // From the start, price may move against the leg before its first new extreme; that counter-move is the leg's first pivot.
        if (from.Kind == EnumPivotKind.Start && runStarts.Count > 0 && runStarts[0] > start)
        {
            var first = start;
            for (var i = start + 1; i < runStarts[0]; i++)
            {
                if (Beats(Counter(prices[first]), Counter(prices[i])))
                    first = i;
            }
            chain.Add(Pivot(counterKind, prices, first, basis));
        }

        for (var r = 0; r < runEnds.Count; r++)
        {
            var isLast = r == runEnds.Count - 1;
            if (isLast && !includeLastExtreme)
                break;

            chain.Add(Pivot(extremeKind, prices, runEnds[r], basis));
            if (isLast)
                break;

            var counter = runEnds[r] + 1;
            for (var i = counter + 1; i < runStarts[r + 1]; i++)
            {
                if (Beats(Counter(prices[counter]), Counter(prices[i])))
                    counter = i;
            }
            chain.Add(Pivot(counterKind, prices, counter, basis));
        }

        return chain;
    }

    private static SawtoothPivot Pivot(EnumPivotKind kind, List<Price> prices, int index, EnumPriceBasis basis)
    {
        var price = prices[index];
        var value = kind switch
        {
            EnumPivotKind.High => HighOf(price, basis),
            EnumPivotKind.Low => LowOf(price, basis),
            _ => price.Close,
        };
        return new SawtoothPivot(kind, index, price.DateTime, value);
    }
}
