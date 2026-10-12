namespace Gradient.CryptoAnalysis;

public static partial class MarketStructure
{
    /// <summary>
    /// The timeline of a store's candles, as <see cref="Timeline(List{Price}, EnumPriceBasis, int, int, int, int?, int?, double, double)"/>
    /// gives it for them, looked up in the store and not worked out: what was known at each candle replayed is what the
    /// store has as known then. The ranges are those of the least retracement and band the store was read with.
    /// </summary>
    public static ReplayTimeline Timeline(StructureStore store, int maxLevel = 8, int from = 0, int? to = null, int? keepFrom = null)
    {
        var end = Math.Min(to ?? store.Candles, store.Candles);
        from = Math.Max(0, from);
        var snapshots = new Snapshot[Math.Max(0, end - from)];

        // The candle of the highest high so far, the first to reach it.
        var high = 0;
        PivotsRead? read = null;
        for (var t = 0; t < end; t++)
        {
            if (store.Price(t, true) > store.Price(high, true))
                high = t;
            if (t < from)
                continue;

            // The pivots are the same as at the candle before unless this one changed any.
            var kept = keepFrom is int k && k > 0 ? Math.Min(k, t) : 0;
            if (read == null || read.Kept != kept || store.PivotsChanged(t))
                read = ReadPivots(store, t, kept, maxLevel);
            snapshots[t - from] = Take(store, t, high, maxLevel, kept, read, t, 0);
        }

        return Track(snapshots, from);
    }

    /// <summary>
    /// A stretch of a store's candles as known at its last candle, shaped as a timeline of that candle alone: what lies at
    /// candles from to to of the whole in hindsight, for a chart that shows that stretch. What is narrower than narrowest
    /// candles is left out, as on a chart zoomed far out it could not be seen: a swing, a market structure break from the
    /// high or low it broke, a trend, a retracement, a run of candles, and a high or low both of whose legs are.
    /// </summary>
    public static ReplayTimeline Complete(StructureStore store, int maxLevel = 8, int from = 0, int? to = null, int narrowest = 0)
    {
        var last = store.Candles - 1;
        if (last < 0)
            return Track([], 0);

        var kept = Math.Clamp(from, 0, last);
        var read = ReadPivots(store, last, kept, maxLevel);
        return Track([Take(store, last, HighOf(read, last), maxLevel, kept, read, Math.Clamp(to ?? last, kept, last), narrowest)], last);
    }

    /// <summary>
    /// Whether the candles up to one have a level: whether any pivot known at its close lies that deep.
    /// </summary>
    public static bool HasLevel(StructureStore store, int level, int at) => level == 0 || store.PivotsBack(at).Any(x => x.Level >= level);

    /// <summary>
    /// A level's confirmed swings as known at the close of a candle, in the order they start: what
    /// <see cref="Sawtooth.Swings"/> makes of the candles to there. With from and to, only those any part of which lies at
    /// those candles.
    /// </summary>
    public static List<SwingOutline> SwingsAt(StructureStore store, int level, int at, int from = 0, int? to = null) =>
        store.Swings(from, to ?? at, at, level).Where(x => x.Level == level).OrderBy(x => x.Start).Select(x => Outline(store, x)).ToList();

    /// <summary>
    /// A level's market structure breaks as known at the close of a candle, in the order of their closes: what
    /// <see cref="Sawtooth.MarketStructureBreaks"/> makes of its swings then.
    /// </summary>
    public static List<MarketStructureBreakOutline> BreaksAt(StructureStore store, int level, int at) =>
        store.Breaks(0, at, at, level).Where(x => x.Level == level).OrderBy(x => x.Break).ThenBy(x => x.Type).Select(x => Outline(store, x).Break).ToList();

    /// <summary>
    /// A level's trends as known at the close of a candle, in the order they came: what <see cref="Sawtooth.Trends"/>
    /// makes of its swings and breaks then. With from and to, only those any part of which lies at those candles, one
    /// that goes on running to the candle known at.
    /// </summary>
    public static List<TrendOutline> TrendsAt(StructureStore store, int level, int at, int from = 0, int? to = null) =>
        store.Trends(from, to ?? at, at, level).Where(x => x.Level == level).OrderBy(x => x.Confirmed).Select(x => Outline(store, x)).ToList();

    /// <summary>
    /// A level's highs and lows as known at the close of a candle, those at candles from a candle on, labelled against the
    /// previous one of their kind: what <see cref="Sawtooth.Points(List{Price}, SawtoothLevel, EnumPriceBasis)"/> makes of
    /// the level then. A level the candles do not have has none.
    /// </summary>
    public static List<StructurePoint> PointsAt(StructureStore store, int level, int at, int from = 0)
    {
        if (level < 0 || at < 0 || at >= store.Candles)
            return [];
        var kept = Math.Clamp(from, 0, at);
        var read = ReadPivots(store, at, kept, level);
        if (read.Deepest < level)
            return [];
        var (held, furthest) = LevelOf(store, read, level, at, HighOf(read, at));
        var keep = kept > 0 ? store.Prices[kept].DateTime : DateTime.MinValue;
        return Sawtooth.Points(store.Prices, new SawtoothLevel(level, held), store.Basis, furthest).Where(x => x.Time >= keep).ToList();
    }

    // The candle of the highest high up to a candle: the pivot of level 0, or the candle itself while that high still runs.
    private static int HighOf(PivotsRead read, int t) => read.ByLevel[0].Count > 0 ? read.ByLevel[0][^1].Index : t;

    // A level's pivots at a candle as the sawtooth lists them, the current price last when the level ends at it, with the
    // furthest point so far of the leg still running to it, the first candle to reach it. The first price begins the level
    // unless the first candle is the highest high, and a highest high on this candle, which can still move and so is not in
    // the store, ends it.
    private static (List<SawtoothPivot> Pivots, int? Furthest) LevelOf(StructureStore store, PivotsRead read, int level, int t, int high)
    {
        var prices = store.Prices;
        var held = new List<SawtoothPivot>(read.ByLevel[level].Count + 3);
        if (read.All && high != 0)
            held.Add(Sawtooth.Pivot(EnumPivotKind.Start, prices, 0, store.Basis) with { ConfirmedIndex = 0 });
        held.AddRange(read.ByLevel[level]);
        if (high == t && !read.ByLevel[0].Any(x => x.Index == t))
            held.Add(Sawtooth.Pivot(EnumPivotKind.High, prices, t, store.Basis));
        if (!(high == t && prices[t].Close == store.Price(t, true)))
            held.Add(Sawtooth.Pivot(EnumPivotKind.Current, prices, t, store.Basis));

        int? furthest = null;
        if (Sawtooth.EndsInAnIncompleteLeg(held) && held[^2].Index < t)
        {
            var up = held[^2].Kind == EnumPivotKind.Low;
            var best = held[^2].Index + 1;
            for (var i = best + 1; i <= t; i++)
            {
                if (up ? store.Price(i, true) > store.Price(best, true) : store.Price(i, false) < store.Price(best, false))
                    best = i;
            }

            furthest = best;
        }

        return (held, furthest);
    }

    private static PricePoint Point(StructureStore store, int candle, bool high) => new(store.Prices[candle].DateTime, store.Price(candle, high));

    private static SwingOutline Outline(StructureStore store, StoredSwing x)
    {
        var up = x.Direction == EnumSwingDirection.Up;
        var broke = Point(store, x.Break, up);
        return new SwingOutline(x.Level, x.Direction, Point(store, x.Start, up), broke, Point(store, x.Extreme, !up), broke);
    }

    private static LevelBreak Outline(StructureStore store, StoredBreak x)
    {
        var above = x.Type == EnumAnnotationType.BullishMarketStructureBreak;
        return new LevelBreak(x.Level, new MarketStructureBreakOutline(x.Type, Point(store, x.Reference, above), Point(store, x.Break, above)));
    }

    private static TrendOutline Outline(StructureStore store, StoredTrend x)
    {
        var up = x.Direction == EnumSwingDirection.Up;
        return new TrendOutline(x.Level, x.Direction, Point(store, x.Start, up), Point(store, x.Confirmed, up), x.End is int end ? Point(store, end, !up) : null, x.Swings,
            x.MarketStructureBreaks, x.Parts.Select(p => new TrendSwing(Point(store, p.Break, up), p.Strong)).ToList());
    }

    // Each level's pivots as the store has them known at a candle, in candle order, with the deepest level any pivot lies
    // at and whether every pivot was read.
    private sealed record PivotsRead(int Kept, List<SawtoothPivot>[] ByLevel, int Deepest, bool All);

    // Each level's pivots are those of its own level and every one above. Of those before the candle kept from, a level
    // needs only the last few: the one a line is drawn from, and those a high or low is labelled against. On wicks a
    // candle can be a high and a low, so a pivot is not always against the one two before it: all are taken.
    private static PivotsRead ReadPivots(StructureStore store, int t, int kept, int maxLevel)
    {
        var prices = store.Prices;
        var few = store.Basis == EnumPriceBasis.Close && kept > 0 ? 9 : int.MaxValue;
        var byLevel = Enumerable.Range(0, maxLevel + 1).Select(_ => new List<SawtoothPivot>()).ToArray();
        var early = new int[maxLevel + 1];
        var (deepest, full, all) = (0, 0, true);
        foreach (var pivot in store.PivotsBack(t))
        {
            if (pivot.Candle < kept && deepest >= maxLevel && full > maxLevel)
            {
                all = false;
                break;
            }

            deepest = Math.Max(deepest, pivot.Level);
            if (pivot.Level > maxLevel)
                continue;
            var read = new SawtoothPivot(pivot.Kind, pivot.Candle, prices[pivot.Candle].DateTime, pivot.Price) { ConfirmedIndex = pivot.Confirmed };
            for (var level = pivot.Level; level <= maxLevel; level++)
            {
                if (pivot.Candle >= kept)
                {
                    byLevel[level].Add(read);
                }
                else if (early[level] < few)
                {
                    byLevel[level].Add(read);
                    if (++early[level] == few)
                        full++;
                }
            }
        }

        foreach (var level in byLevel)
            level.Reverse();
        return new PivotsRead(kept, byLevel, deepest, all);
    }

    // Everything the replay tracks at a candle, as the store has it known then, leaving out what ended before the candle
    // kept from: what TakeSnapshot works out from the candles to there. With a candle before it to read up to, what
    // begins after that is left out too, and with a narrowest width, what is narrower (see Complete).
    private static Snapshot Take(StructureStore store, int t, int high, int maxLevel, int kept, PivotsRead read, int upTo, int narrowest)
    {
        var prices = store.Prices;
        var keep = kept > 0 ? prices[kept].DateTime : DateTime.MinValue;
        var until = prices[upTo].DateTime;
        PricePoint Point(int candle, bool isHigh) => new(prices[candle].DateTime, store.Price(candle, isHigh));

        var deepest = read.Deepest;
        var count = Math.Min(maxLevel, deepest) + 1;
        var pivots = new List<LevelPivot>();
        var points = new List<StructurePoint>();
        for (var level = 0; level < count; level++)
        {
            var (held, furthest) = LevelOf(store, read, level, t, high);
            var turns = held[^1].Kind == EnumPivotKind.Current ? held.Count - 1 : held.Count;
            // A line runs from the pivot before the stretch to the one after it.
            var first = keep == DateTime.MinValue ? 0 : Math.Max(0, held.FindLastIndex(turns - 1, turns, p => p.Time < keep));
            var end = held.FindIndex(0, turns, p => p.Index > upTo);
            end = end < 0 ? turns - 1 : end;
            // Of the pivots left out as too narrow, the highest and the lowest of each stretch as wide as the narrowest
            // seen are kept: a run of narrow legs can climb or fall a long way between them, and with all of its pivots
            // gone the line would cut straight across from before it to after it, missing its highs and lows.
            var furthestOf = new HashSet<int>();
            if (narrowest > 1)
            {
                foreach (var stretch in Enumerable.Range(first, end - first + 1).GroupBy(i => held[i].Index / narrowest))
                {
                    furthestOf.Add(stretch.MaxBy(i => held[i].Price));
                    furthestOf.Add(stretch.MinBy(i => held[i].Price));
                }
            }

            for (var i = first; i <= end; i++)
            {
                // A pivot both of whose legs are too narrow to see is left out, but for the ends of the line.
                if (narrowest <= 1 || i == first || i == end || held[i].Index - held[i - 1].Index >= narrowest || held[i + 1].Index - held[i].Index >= narrowest || furthestOf.Contains(i))
                    pivots.Add(new LevelPivot(level, held[i]));
            }

            var ofLevel = Sawtooth.Points(prices, new SawtoothLevel(level, held), store.Basis, furthest);
            for (var i = 0; i < ofLevel.Count; i++)
            {
                var point = ofLevel[i];
                if (point.Time < keep || point.Time > until)
                    continue;
                if (narrowest <= 1 || Candles(point.LegStart, point.Time) >= narrowest || (i + 1 < ofLevel.Count && Candles(point.Time, ofLevel[i + 1].Time) >= narrowest))
                    points.Add(point);
            }
        }

        var top = Math.Min(maxLevel, deepest + 1);
        var swings = store.Swings(kept, upTo, t, top).Where(x => x.Break - x.Start >= narrowest).Select(x => Outline(store, x)).ToList();
        var candidates = store.Candidates(t, top).Where(x => x.Start <= upTo).Select(x =>
        {
            var up = x.Direction == EnumSwingDirection.Up;
            return new CandidateSwing(x.Level, x.Direction, Point(x.Start, up), Point(x.Extreme, !up));
        }).ToList();
        var breaks = store.Breaks(kept, upTo, t, top).Where(x => x.Break - x.Reference >= narrowest).Select(x => Outline(store, x)).ToList();
        var trends = store.Trends(kept, upTo, t, top).Where(x => (x.End ?? t) - x.Start >= narrowest).Select(x => Outline(store, x)).ToList();
        var retracements = store.Retracements(kept, upTo, t, top).Where(x => x.Steps[^1].Candle - x.From >= narrowest).Select(x =>
        {
            var up = x.Direction == EnumSwingDirection.Up;
            return new RetracementOutline(x.Level, x.Direction, Point(x.From, !up), Point(x.To, up),
                x.Steps.Select(s => new RetracementStep(Point(s.Candle, !up), s.Percent)).ToList(), x.Confirmed);
        }).ToList();
        // A range needs the structure one level finer, so the finest level has none.
        var ranges = store.Ranges(kept, upTo, t, top - 1).Select(x =>
        {
            var up = x.Direction == EnumSwingDirection.Up;
            var range = new RangeOutline(x.Level, x.Direction, Point(x.From, !up), Point(x.Low, false), Point(x.High, true), Point(x.Identified, up), x.Retracement, x.Band, null);
            return x.End is int end ? range with { End = Point(end, store.Price(end, true) > range.BandHigh) } : range;
        }).ToList();

        var runs = CandleRuns.RunsFrom(t == prices.Count - 1 ? prices : prices.GetRange(0, t + 1), minLength: Math.Max(2, narrowest), kept, upTo);
        return new Snapshot(pivots, points, runs.Where(x => x.End.Time >= keep).ToList(), swings, candidates, breaks, trends, retracements, ranges);

        // How many candles lie from a time to a later one.
        int Candles(DateTime start, DateTime finish) => IndexOf(prices, finish) - IndexOf(prices, start);
    }

    // The candle of a time: the first at or after it.
    private static int IndexOf(List<Price> prices, DateTime time)
    {
        var (low, high) = (0, prices.Count);
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (prices[middle].DateTime < time)
                low = middle + 1;
            else
                high = middle;
        }

        return low;
    }
}