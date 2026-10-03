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
/// A turning point on a sawtooth. Start is the first price, which is neither a high nor a low; Current is the last price,
/// which ends the final, incomplete leg. Index is the candle index.
/// ConfirmedIndex is the candle from which the point can no longer move: for a high or low that ends a run of new extremes,
/// the candle after it; for a pullback low or bounce high, the candle that starts the next run. It is null while the point can
/// still move (the current price, or an extreme on the last candle). Its level can still change, because a later new high redraws
/// the levels.
/// A pivot is not the same as a structure point: a pivot needs price to turn away from it, but a high is a high (and an HH is an
/// HH) the moment price makes it. See <see cref="Sawtooth.Points"/>.
/// </summary>
public sealed record SawtoothPivot(EnumPivotKind Kind, int Index, DateTime Time, double Price)
{
    public int? ConfirmedIndex { get; init; }
}

/// <summary>
/// A high or low at a level, as currently known, with its label: H or L for the first of its kind, HH, LH, HL or LL against the
/// previous one of its kind (Type is then the matching term), and EQH or EQL when equal. Provisional is true while price can
/// still extend it, which moves it: a new higher close moves an HH forward. LegStart is the time of the point its leg starts
/// from, which stays the same while the point moves.
/// </summary>
public sealed record StructurePoint(
    int Level,
    EnumPivotKind Kind,
    DateTime Time,
    double Price,
    string Label,
    EnumAnnotationType? Type,
    DateTime LegStart,
    bool Provisional);

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
    /// in a downleg the mirror. A leg's last high or low that has not been broken is not yet a swing. In the final, incomplete
    /// leg a break on the last candle counts, so a swing is known on the candle that breaks structure.
    /// </summary>
    public static List<SwingOutline> Swings(List<Price> prices, IReadOnlyList<SawtoothLevel> levels, EnumPriceBasis basis, int level)
    {
        var swings = new List<SwingOutline>();
        if (level < 1 || level >= levels.Count)
            return swings;

        var parent = levels[level - 1].Pivots;
        var child = levels[level].Pivots.Where(p => p.Kind is EnumPivotKind.High or EnumPivotKind.Low).ToList();
        var last = prices.Count - 1;
        var first = 0;

        for (var i = 0; i + 1 < parent.Count; i++)
        {
            var from = parent[i];
            var to = parent[i + 1];
            var end = to.Kind == EnumPivotKind.Current ? last : to.Index;
            var up = to.Kind == EnumPivotKind.High || (to.Kind == EnumPivotKind.Current && from.Kind == EnumPivotKind.Low);
            var extremeKind = up ? EnumPivotKind.High : EnumPivotKind.Low;

            // Pivots are in candle order and legs follow on from each other, so one pass finds each leg's turns.
            while (first < child.Count && child[first].Index < from.Index)
                first++;
            var turns = new List<SawtoothPivot>();
            for (var k = first; k < child.Count && child[k].Index <= end; k++)
                turns.Add(child[k]);
            for (var t = 0; t + 1 < turns.Count; t++)
            {
                var start = turns[t];
                var counter = turns[t + 1];
                if (start.Kind != extremeKind)
                    continue;

                // The next extreme bounds the search; in the final leg an extreme still running on the last candle is not a
                // pivot yet, so the search runs to the last candle.
                int limit;
                if (t + 2 < turns.Count)
                    limit = turns[t + 2].Index;
                else if (to.Kind == EnumPivotKind.Current)
                    limit = last;
                else
                    continue;

                for (var c = counter.Index + 1; c <= limit; c++)
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
    /// Returns a level's highs and lows as currently known, labelled against the previous one of their kind. These are the
    /// level's high and low pivots plus, when the level ends in an incomplete leg, that leg's furthest point so far: it is a high
    /// (or low) now, whether or not price has turned away from it, and it moves while price extends the leg.
    /// </summary>
    public static List<StructurePoint> Points(List<Price> prices, SawtoothLevel level, EnumPriceBasis basis)
    {
        var pivots = level.Pivots;
        var extremes = new List<(EnumPivotKind Kind, DateTime Time, double Price, DateTime LegStart, bool Provisional)>();
        for (var i = 0; i < pivots.Count; i++)
        {
            var p = pivots[i];
            if (p.Kind is EnumPivotKind.High or EnumPivotKind.Low)
                extremes.Add((p.Kind, p.Time, p.Price, i > 0 ? pivots[i - 1].Time : p.Time, p.ConfirmedIndex == null));
        }

        // The incomplete leg's furthest point so far: the first candle to reach it, since an equal close does not extend it.
        if (pivots.Count >= 2 && pivots[^1].Kind == EnumPivotKind.Current && pivots[^2].Kind is EnumPivotKind.High or EnumPivotKind.Low)
        {
            var from = pivots[^2];
            var up = from.Kind == EnumPivotKind.Low;
            var best = from.Index + 1;
            for (var i = best + 1; i < prices.Count; i++)
            {
                if (up ? HighOf(prices[i], basis) > HighOf(prices[best], basis) : LowOf(prices[i], basis) < LowOf(prices[best], basis))
                    best = i;
            }

            if (best < prices.Count)
                extremes.Add((up ? EnumPivotKind.High : EnumPivotKind.Low, prices[best].DateTime,
                    up ? HighOf(prices[best], basis) : LowOf(prices[best], basis), from.Time, true));
        }

        var points = new List<StructurePoint>();
        double? lastHigh = null, lastLow = null;
        foreach (var e in extremes)
        {
            var high = e.Kind == EnumPivotKind.High;
            var previous = high ? lastHigh : lastLow;
            var (label, type) = previous switch
            {
                null => (high ? "H" : "L", (EnumAnnotationType?)null),
                double p when e.Price == p => (high ? "EQH" : "EQL", null),
                double p when e.Price > p => (high ? "HH" : "HL", high ? EnumAnnotationType.HigherHigh : EnumAnnotationType.HigherLow),
                _ => (high ? "LH" : "LL", high ? EnumAnnotationType.LowerHigh : EnumAnnotationType.LowerLow),
            };
            points.Add(new StructurePoint(level.Level, e.Kind, e.Time, e.Price, label, type, e.LegStart, e.Provisional));
            if (high)
                lastHigh = e.Price;
            else
                lastLow = e.Price;
        }

        return points;
    }

    /// <summary>
    /// Returns the candidate swings at a level (1 or above): a swing that has its start and a pullback (or bounce) so far,
    /// but no break of structure yet. It is not a swing: it becomes one if price breaks its start before a new high redraws the levels,
    /// at this level, or a coarser one when the same candle also breaks a coarser level's extreme. Candidates only exist in
    /// the final, incomplete leg: its last high (in an upleg) or low (in a downleg) with the counter-move since; and at level 1,
    /// the series high with the pullback since, which a new high would break.
    /// </summary>
    public static List<CandidateSwing> Candidates(List<Price> prices, IReadOnlyList<SawtoothLevel> levels, EnumPriceBasis basis, int level)
    {
        var candidates = new List<CandidateSwing>();
        if (level < 1 || levels.Count == 0)
            return candidates;

        var last = prices.Count - 1;
        var parent = levels[Math.Min(level - 1, levels.Count - 1)].Pivots;
        var child = levels[Math.Min(level, levels.Count - 1)].Pivots;
        if (parent.Count < 2 || parent[^1].Kind != EnumPivotKind.Current || child.Count < 2)
            return candidates;

        // The series high with the pullback since: a close above it breaks structure at level 1. A high on the first candle
        // becomes the start when it is broken, not a high, so it has no candidate.
        var from = parent[^2];
        if (level == 1 && from.Kind == EnumPivotKind.High && parent[0].Kind == EnumPivotKind.Start)
            AddCandidate(from, up: true);

        // The final leg's last extreme with the counter-move since.
        var up = from.Kind == EnumPivotKind.Low;
        var extreme = child[^2];
        if (extreme.Index != from.Index && extreme.Kind == (up ? EnumPivotKind.High : EnumPivotKind.Low))
            AddCandidate(extreme, up);

        return candidates;

        void AddCandidate(SawtoothPivot start, bool up)
        {
            if (start.Index >= last)
                return;

            var counter = start.Index + 1;
            for (var i = start.Index + 1; i <= last; i++)
            {
                var high = HighOf(prices[i], basis);
                var low = LowOf(prices[i], basis);
                if (up ? high > start.Price : low < start.Price)
                    return;
                if (up ? low < LowOf(prices[counter], basis) : high > HighOf(prices[counter], basis))
                    counter = i;
            }

            var counterPrice = up ? LowOf(prices[counter], basis) : HighOf(prices[counter], basis);
            candidates.Add(new CandidateSwing(level, up ? EnumSwingDirection.Up : EnumSwingDirection.Down,
                new PricePoint(start.Time, start.Price), new PricePoint(prices[counter].DateTime, counterPrice)));
        }
    }

    /// <summary>
    /// Returns the retracements at a level: for each confirmed swing, and each candidate, that has a confirmed swing in the same
    /// direction before it, how far its counter-move came back along the move from that swing's extreme to this one's start,
    /// reading by reading (see <see cref="RetracementOutline"/>). The first swing in a leg has nothing to measure against.
    /// </summary>
    public static List<RetracementOutline> Retracements(List<Price> prices, IReadOnlyList<SwingOutline> swings, IReadOnlyList<CandidateSwing> candidates, EnumPriceBasis basis)
    {
        var index = new Dictionary<DateTime, int>();
        for (var i = 0; i < prices.Count; i++)
            index.TryAdd(prices[i].DateTime, i);

        var confirmed = swings.Where(x => x.BreakOfStructure != null).OrderBy(x => x.BreakOfStructure!.Time).ToList();
        var retracements = new List<RetracementOutline>();

        foreach (var swing in confirmed)
            Add(swing.Level, swing.Direction, swing.Start, index[swing.Extreme.Time], true);
        foreach (var candidate in candidates)
            Add(candidate.Level, candidate.Direction, candidate.Start, prices.Count - 1, false);

        return retracements.OrderBy(x => x.To.Time).ToList();

        void Add(int level, EnumSwingDirection direction, PricePoint to, int end, bool final)
        {
            // The last swing to break structure before this one starts; one the other way means this is the first swing of its leg.
            var previous = confirmed.LastOrDefault(x => x.Level == level && x.BreakOfStructure!.Time <= to.Time && x.Start.Time < to.Time);
            if (previous == null || previous.Direction != direction)
                return;

            var from = previous.Extreme;
            var up = direction == EnumSwingDirection.Up;
            var gap = up ? to.Price - from.Price : from.Price - to.Price;
            var start = index[to.Time];
            if (gap <= 0 || start >= end)
                return;

            var steps = new List<RetracementStep>();
            double? best = null;
            for (var i = start + 1; i <= end; i++)
            {
                var value = up ? LowOf(prices[i], basis) : HighOf(prices[i], basis);
                if (best is double b && !(up ? value < b : value > b))
                    continue;
                best = value;
                var back = up ? to.Price - value : value - to.Price;
                steps.Add(new RetracementStep(new PricePoint(prices[i].DateTime, value), (int)Math.Floor(100 * back / gap)));
            }

            if (steps.Count > 0)
                retracements.Add(new RetracementOutline(level, direction, from, to, steps, final));
        }
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

    /// <summary>
    /// Returns the trends in a level's swings: runs of at least minSwings consecutive swings in the same direction, taken in
    /// the order they broke structure. A trend is confirmed by the break of structure of its minSwings-th swing and ends when a
    /// swing in the other direction breaks structure; until then it is ongoing. Market structure breaks against the trend are
    /// counted but do not end it: one that falls inside a swing, between its start and its break of structure, makes that
    /// swing a weak one.
    /// </summary>
    public static List<TrendOutline> Trends(IReadOnlyList<SwingOutline> swings, IReadOnlyList<MarketStructureBreakOutline> breaks, int minSwings = 2)
    {
        var ordered = swings.Where(x => x.BreakOfStructure != null).OrderBy(x => x.BreakOfStructure!.Time).ToList();
        var trends = new List<TrendOutline>();

        for (var i = 0; i < ordered.Count;)
        {
            var j = i;
            while (j + 1 < ordered.Count && ordered[j + 1].Direction == ordered[i].Direction)
                j++;

            if (j - i + 1 >= Math.Max(1, minSwings))
            {
                var first = ordered[i];
                var end = j + 1 < ordered.Count ? ordered[j + 1].BreakOfStructure : null;
                var against = first.Direction == EnumSwingDirection.Up ? EnumAnnotationType.BearishMarketStructureBreak : EnumAnnotationType.BullishMarketStructureBreak;
                var msbs = breaks.Count(b => b.Type == against && b.Break.Time > first.BreakOfStructure!.Time && (end == null || b.Break.Time <= end.Time));
                var parts = ordered.GetRange(i, j - i + 1)
                    .Select(s => new TrendSwing(s.BreakOfStructure!, !breaks.Any(b => b.Type == against && b.Break.Time > s.Start.Time && b.Break.Time < s.BreakOfStructure!.Time)))
                    .ToList();
                trends.Add(new TrendOutline(first.Level, first.Direction, first.Start, ordered[i + Math.Max(1, minSwings) - 1].BreakOfStructure!, end, j - i + 1, msbs, parts));
            }

            i = j + 1;
        }

        return trends;
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
        var athPivot = Pivot(EnumPivotKind.High, prices, ath, basis) with { ConfirmedIndex = ath < last ? ath + 1 : null };

        // When the first candle is the high, the start and the high are the same point.
        if (ath != 0)
            pivots.Add(Pivot(EnumPivotKind.Start, prices, 0, basis) with { ConfirmedIndex = 0 });
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
            chain.Add(Pivot(counterKind, prices, first, basis) with { ConfirmedIndex = runStarts[0] });
        }

        // An extreme is fixed once its run ends (the next candle); a counter-move once the next run starts.
        for (var r = 0; r < runEnds.Count; r++)
        {
            var isLast = r == runEnds.Count - 1;
            if (isLast && !includeLastExtreme)
                break;

            chain.Add(Pivot(extremeKind, prices, runEnds[r], basis) with { ConfirmedIndex = runEnds[r] + 1 < prices.Count ? runEnds[r] + 1 : null });
            if (isLast)
                break;

            var counter = runEnds[r] + 1;
            for (var i = counter + 1; i < runStarts[r + 1]; i++)
            {
                if (Beats(Counter(prices[counter]), Counter(prices[i])))
                    counter = i;
            }
            chain.Add(Pivot(counterKind, prices, counter, basis) with { ConfirmedIndex = runStarts[r + 1] });
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
