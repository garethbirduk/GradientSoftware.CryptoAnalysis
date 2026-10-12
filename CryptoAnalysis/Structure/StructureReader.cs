namespace Gradient.CryptoAnalysis;

/// <summary>
/// A market structure break by its candles: the swing whose protective high or low was broken, the candle of that high or
/// low, and the candle of the first close beyond it.
/// </summary>
public sealed record ReadBreak(long Id, ReadSwing Swing, EnumAnnotationType Type, int Reference, int Break);

/// <summary>
/// A trend by the swing it begins with. What else there is to it changes as it goes on: see <see cref="TrendState"/>.
/// </summary>
public sealed record ReadTrend(long Id, EnumSwingDirection Direction, ReadSwing First);

/// <summary>
/// One swing of a trend: the candle of its break of structure, and whether it is a strong one.
/// </summary>
public readonly record struct TrendPart(int Break, bool Strong);

/// <summary>
/// A trend as it stands at a candle: its level, the candle of the break of structure that confirmed it, the candle of the
/// break of structure that ended it (none while it goes on), its swings, and the market structure breaks against it.
/// </summary>
public readonly record struct TrendState(int Level, int Confirmed, int? End, int MarketStructureBreaks, IReadOnlyList<TrendPart> Parts)
{
    public int Swings => Parts.Count;

    /// <summary>
    /// Compares two states by value, their swings included.
    /// </summary>
    public bool Equals(TrendState other) => Level == other.Level && Confirmed == other.Confirmed && End == other.End
        && MarketStructureBreaks == other.MarketStructureBreaks && Parts.SequenceEqual(other.Parts);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Level, Confirmed, End, MarketStructureBreaks, Parts.Count);
}

/// <summary>
/// A retracement by its candles: the candle of the previous swing's extreme, the candle this swing starts at, and the
/// candles of each new furthest point of the counter-move since, in order. End is the candle of the swing's extreme, where
/// the reading ends; a candidate's has none, and its steps are added to while it stays one.
/// </summary>
public sealed record ReadRetracement(long Id, EnumSwingDirection Direction, int From, int To, int? End, IReadOnlyList<int> Steps)
{
    public bool Confirmed => End != null;
}

/// <summary>
/// A candidate swing by its candles: the candle it starts at, and the candles of each new furthest point of the
/// counter-move since, in order, the last being its extreme so far. The steps are added to while it stays a candidate.
/// </summary>
public sealed record ReadCandidate(long Id, EnumSwingDirection Direction, int Start, IReadOnlyList<int> Steps);

/// <summary>
/// A range by its candles: where the move it retraces began, its low and its high, the candle of the market structure
/// break one level finer that identified it, the reading then, and the band it ends beyond.
/// </summary>
public sealed record ReadRange(long Id, EnumSwingDirection Direction, int From, int Low, int High, int Identified, int Retracement, double Band);

/// <summary>
/// A range as it stands at a candle: its level, and the candle of the close that ended it (none while it holds).
/// </summary>
public readonly record struct RangeState(int Level, int? End);

/// <summary>
/// A market structure break that came, changed level or went on a candle: no level when it is one no longer.
/// </summary>
public sealed record BreakChange(ReadBreak Break, int? Level);

/// <summary>
/// A trend that came, changed or went on a candle: no state when it is one no longer.
/// </summary>
public sealed record TrendChange(ReadTrend Trend, TrendState? State);

/// <summary>
/// A retracement that came, changed level or went on a candle: no level when it is one no longer.
/// </summary>
public sealed record RetracementChange(ReadRetracement Retracement, int? Level);

/// <summary>
/// A candidate swing that came, changed level or went on a candle: no level when it is one no longer.
/// </summary>
public sealed record CandidateChange(ReadCandidate Candidate, int? Level);

/// <summary>
/// A range that came, changed or went on a candle: no state when it is one no longer.
/// </summary>
public sealed record RangeChange(ReadRange Range, RangeState? State);

/// <summary>
/// What a candle changed of the structure: see <see cref="StructureReader"/>.
/// </summary>
public sealed record StructureChanges(
    IReadOnlyList<PivotChange> Pivots,
    IReadOnlyList<SwingChange> Swings,
    IReadOnlyList<BreakChange> Breaks,
    IReadOnlyList<TrendChange> Trends,
    IReadOnlyList<RetracementChange> Retracements,
    IReadOnlyList<CandidateChange> Candidates,
    IReadOnlyList<RangeChange> Ranges)
{
    public static readonly StructureChanges None = new([], [], [], [], [], [], []);
}

/// <summary>
/// Reads market structure a candle at a time: the sawtooth's pivots and swings (see <see cref="SawtoothReader"/>) and, of
/// the swings sharing a level, its market structure breaks, trends, retracements, candidate swings and ranges. After each
/// candle these are what <see cref="Sawtooth"/> and <see cref="Ranges"/> would make of the candles so far, and
/// <see cref="Changes"/> is what the candle changed of them.
/// Each level's swings are kept in the order they broke structure. What a candle changes of them is nearly always at the
/// end, so only what follows from the first swing changed is worked out again: the breaks from the swing before it each
/// way, the trends from the run it is in, the retracements from it on, and the ranges of the retracements that start from
/// there. A candle that changes no swing is read against the last swing each way, the candidates and the range that holds.
/// Everything is by candle, counted from the first read, and an item keeps its id whenever it is found again.
/// </summary>
public sealed class StructureReader(EnumPriceBasis basis, int minTrendSwings = 2, double minRetracement = Ranges.DefaultMinRetracement,
    double band = Ranges.DefaultBand)
{
    // In the order they broke structure, and by where they start when two broke on a candle.
    private static readonly Comparer<Held> Order = Comparer<Held>.Create((a, b) =>
        a.Break != b.Break ? a.Break.CompareTo(b.Break)
        : a.Start != b.Start ? a.Start.CompareTo(b.Start)
        : a.Up != b.Up ? (a.Up ? -1 : 1)
        : a.Extreme != b.Extreme ? a.Extreme.CompareTo(b.Extreme)
        : a.Read.Leg.CompareTo(b.Read.Leg));

    // In the order of the closes beyond them.
    private static readonly Comparer<Held> Broke = Comparer<Held>.Create((a, b) =>
        a.BeyondAt != b.BeyondAt ? a.BeyondAt.CompareTo(b.BeyondAt) : Order.Compare(a, b));

    private readonly SawtoothReader _reader = new(basis);
    private readonly int _least = Math.Max(1, minTrendSwings);
    private readonly Dictionary<ReadSwing, Held> _swings = [];

    // Level 1 is the first.
    private readonly List<Level> _levels = [];

    private readonly Kind<ReadBreak, int> _breaks = new();
    private readonly Kind<ReadTrend, TrendState> _trends = new();
    private readonly Kind<ReadRetracement, int> _retracements = new();
    private readonly Kind<ReadCandidate, int> _candidates = new();
    private readonly Kind<ReadRange, RangeState> _ranges = new();

    // Each item there has been, so that one found again is the same one.
    private readonly Dictionary<(ReadSwing Swing, int From), ReadRetracement?> _confirmed = [];
    private readonly Dictionary<(bool Up, int Start), Open> _open = [];
    private readonly Dictionary<(long Candidate, int From), ReadRetracement> _opened = [];
    private readonly Dictionary<(EnumSwingDirection Direction, int From, int Low, int High, int Identified, int Retracement), Found> _found = [];
    private long _breakIds, _trendIds, _retracementIds, _candidateIds;

    /// <summary>
    /// How many candles have been read.
    /// </summary>
    public int Count => _reader.Count;

    /// <summary>
    /// What the last candle read changed.
    /// </summary>
    public StructureChanges Changes { get; private set; } = StructureChanges.None;

    /// <summary>
    /// Reads the next candle.
    /// </summary>
    public void Add(Price price)
    {
        _reader.Add(price);
        var last = _reader.Count - 1;
        foreach (var change in _reader.Changes.Swings)
            Move(change);
        LevelAt(_reader.Depth);

        // A level's ranges are read from the breaks one level finer, so every level's are found first.
        foreach (var level in _levels)
            Read(level, last);
        foreach (var level in _levels)
            ReadRanges(level, last);
        foreach (var level in _levels)
            level.Dirty = level.Moved = level.BrokenFrom = level.Reread = int.MaxValue;

        Changes = new StructureChanges(
            _reader.Changes.Pivots,
            _reader.Changes.Swings,
            _breaks.Take(x => x.Id).Select(x => new BreakChange(x.Item, x.State)).ToList(),
            _trends.Take(x => x.Id).Select(x => new TrendChange(x.Item, x.State)).ToList(),
            _retracements.Take(x => x.Id).Select(x => new RetracementChange(x.Item, x.State)).ToList(),
            _candidates.Take(x => x.Id).Select(x => new CandidateChange(x.Item, x.State)).ToList(),
            _ranges.Take(x => x.Id).Select(x => new RangeChange(x.Item, x.State)).ToList());
    }

    private Level? LevelAt(int number)
    {
        while (_levels.Count < number)
            _levels.Add(new Level(_levels.Count + 1));
        return number >= 1 ? _levels[number - 1] : null;
    }

    private double Price(int candle, bool high) => _reader.Extreme(candle, high);

    // A swing leaves the level it lay at, with what it had there, and joins the one it lies at now.
    private void Move(SwingChange change)
    {
        if (!_swings.TryGetValue(change.Swing, out var swing))
            _swings[change.Swing] = swing = new Held(change.Swing);

        if (LevelAt(swing.Level) is { } from)
        {
            var at = from.Swings.BinarySearch(swing, Order);
            from.Swings.RemoveAt(at);
            from.Dirty = Math.Min(from.Dirty, at);
            from.Moved = Math.Min(from.Moved, swing.Start);
            if (swing.Broken)
                Mark(from, swing, false);
            if (swing.Item != null)
                _breaks.Drop(swing.Item);
            if (swing.Retracement != null)
                _retracements.Drop(swing.Retracement);
            swing.Retracement = null;
            if (swing.Heads)
            {
                _trends.Drop(swing.Trend!);
                from.Heads.Remove(swing);
                swing.Heads = false;
            }
        }

        swing.Level = change.Level ?? -1;
        if (LevelAt(swing.Level) is { } to)
        {
            var at = ~to.Swings.BinarySearch(swing, Order);
            to.Swings.Insert(at, swing);
            to.Dirty = Math.Min(to.Dirty, at);
            to.Moved = Math.Min(to.Moved, swing.Start);
        }
    }

    // A level's breaks, retracements, trends and candidates at the last candle.
    private void Read(Level level, int last)
    {
        var swings = level.Swings;
        var cut = int.MaxValue;
        if (level.Dirty != int.MaxValue)
        {
            // A swing protects the pivot against it until the next its way breaks structure, so the last swing each way
            // before the first changed is read again, and every one after.
            cut = level.Dirty;
            for (var (i, seen) = (level.Dirty - 1, 0); i >= 0 && seen != 3; i--)
            {
                var way = swings[i].Up ? 1 : 2;
                if ((seen & way) != 0)
                    continue;
                seen |= way;
                cut = i;
            }

            Held? up = null, down = null;
            (level.Last[0], level.Last[1]) = (null, null);
            for (var i = swings.Count - 1; i >= cut; i--)
            {
                var swing = swings[i];
                ref var next = ref swing.Up ? ref up : ref down;
                Protect(level, swing, next, last);
                next = swing;
                level.Last[swing.Up ? 0 : 1] ??= swing;
            }

            for (var i = level.Dirty; i < swings.Count; i++)
            {
                var swing = swings[i];
                var previous = Before(swings, swing.Start);
                var retracement = previous != null && previous.Up == swing.Up ? Confirmed(swing, previous.Extreme) : null;
                if (swing.Retracement != null && swing.Retracement != retracement)
                    _retracements.Drop(swing.Retracement);
                swing.Retracement = retracement;
                if (retracement != null)
                    _retracements.Put(retracement, level.Number);
                level.Moved = Math.Min(level.Moved, swing.Start);
            }
        }
        else
        {
            // Only the last swing each way still protects its pivot.
            foreach (var swing in level.Last)
            {
                if (swing is { Broken: false } && Protect(level, swing, null, last))
                    cut = swings.Count;
            }
        }

        if (cut != int.MaxValue)
            ReadTrends(level, cut, level.Dirty != int.MaxValue);

        // The candidates, each with its retracement against the last swing to break structure before it starts.
        var candidates = new List<(ReadCandidate Candidate, ReadRetracement? Retracement)>();
        foreach (var (up, start, _) in _reader.CandidatesAt(level.Number))
        {
            var candidate = Candidate(up, start.Index, last);
            var previous = Before(swings, start.Index);
            candidates.Add((candidate, previous != null && previous.Up == up ? Opened(candidate, previous.Extreme) : null));
        }

        if (!candidates.SequenceEqual(level.Candidates))
        {
            foreach (var (candidate, retracement) in level.Candidates)
            {
                _candidates.Drop(candidate);
                if (retracement != null)
                    _retracements.Drop(retracement);
                level.Reread = Math.Min(level.Reread, candidate.Start);
            }

            foreach (var (candidate, _) in candidates)
                level.Reread = Math.Min(level.Reread, candidate.Start);
            level.Candidates = candidates;
        }

        foreach (var (candidate, retracement) in candidates)
        {
            _candidates.Put(candidate, level.Number);
            if (retracement != null)
                _retracements.Put(retracement, level.Number);
        }
    }

    // Whether a swing's pivot has been closed beyond while it protects it: up to the candle before the next swing its way
    // breaks structure, or to the last candle when there is none. Returns whether that has changed.
    private bool Protect(Level level, Held swing, Held? next, int last)
    {
        var to = next != null ? next.Break - 1 : last;
        var up = swing.Up;
        var against = Price(swing.Extreme, !up);
        for (var c = Math.Max(swing.Break, swing.BeyondRead) + 1; swing.BeyondAt < 0 && c <= to; c++)
        {
            var value = Price(c, !up);
            if (up ? value < against : value > against)
                swing.BeyondAt = c;
        }

        if (swing.BeyondAt < 0)
            swing.BeyondRead = Math.Max(swing.BeyondRead, to);
        var broken = swing.BeyondAt >= 0 && swing.BeyondAt <= to;
        var changed = broken != swing.Broken;
        if (changed)
            Mark(level, swing, broken);
        if (broken)
        {
            swing.Item ??= new ReadBreak(++_breakIds, swing.Read,
                up ? EnumAnnotationType.BearishMarketStructureBreak : EnumAnnotationType.BullishMarketStructureBreak, swing.Extreme, swing.BeyondAt);
            _breaks.Put(swing.Item, level.Number);
        }
        else if (swing.Item != null)
        {
            _breaks.Drop(swing.Item);
        }

        return changed;
    }

    // A swing's pivot is broken at its level, or is so no longer.
    private static void Mark(Level level, Held swing, bool broken)
    {
        var breaks = level.Broken[swing.Up ? 0 : 1];
        var at = breaks.BinarySearch(swing, Broke);
        if (broken)
            breaks.Insert(~at, swing);
        else
            breaks.RemoveAt(at);
        swing.Broken = broken;
        level.BrokenFrom = Math.Min(level.BrokenFrom, swing.BeyondAt);
    }

    // The trends from the run of swings that the swing before a place is in: a run of at least the fewest swings one way,
    // confirmed by the break of structure of the last of those and ended by the first swing the other way.
    private void ReadTrends(Level level, int cut, bool moved)
    {
        var swings = level.Swings;
        var heads = level.Heads;
        var were = new List<Held>();
        var first = Math.Max(0, Math.Min(cut, swings.Count) - 1);
        while (first > 0 && swings[first - 1].Up == swings[first].Up)
            first--;
        while (heads.Count > 0 && (swings.Count == 0 || Order.Compare(heads[^1], swings[first]) >= 0))
        {
            were.Add(heads[^1]);
            heads[^1].Heads = false;
            heads.RemoveAt(heads.Count - 1);
        }

        // Which trend holds at a time is the same as it was up to the first swing read again.
        if (moved)
            level.Reread = Math.Min(level.Reread, swings.Count > 0 ? swings[first].Break : 0);

        for (var i = first; i < swings.Count;)
        {
            var j = i;
            while (j + 1 < swings.Count && swings[j + 1].Up == swings[i].Up)
                j++;

            if (j - i + 1 >= _least)
            {
                // The breaks against it after its first break of structure, up to its end; and a swing is strong when
                // none falls between its start and its break of structure.
                var head = swings[i];
                var against = level.Broken[head.Up ? 0 : 1];
                int? end = j + 1 < swings.Count ? swings[j + 1].Break : null;
                var breaks = (end is int e ? UpTo(against, e) : against.Count) - UpTo(against, head.Break);
                var parts = new TrendPart[j - i + 1];
                for (var k = i; k <= j; k++)
                    parts[k - i] = new TrendPart(swings[k].Break, UpTo(against, swings[k].Break - 1) - UpTo(against, swings[k].Start) <= 0);

                (head.Heads, head.Confirmed, head.End) = (true, swings[i + _least - 1].Break, end ?? -1);
                heads.Add(head);
                head.Trend ??= new ReadTrend(++_trendIds, head.Read.Direction, head.Read);
                _trends.Put(head.Trend, new TrendState(level.Number, head.Confirmed, end, breaks, parts));
            }

            i = j + 1;
        }

        foreach (var head in were.Where(x => !x.Heads))
            _trends.Drop(head.Trend!);
    }

    // How many of the swings, in the order of the closes beyond their pivots, were closed beyond at or before a candle.
    private static int UpTo(List<Held> broken, int candle)
    {
        var (low, high) = (0, broken.Count);
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (broken[middle].BeyondAt <= candle)
                low = middle + 1;
            else
                high = middle;
        }

        return low;
    }

    // The last swing to break structure at or before a candle that also starts before it: nearly always the last to break.
    private static Held? Before(List<Held> swings, int candle)
    {
        var (low, high) = (0, swings.Count);
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (swings[middle].Break <= candle)
                low = middle + 1;
            else
                high = middle;
        }

        for (var i = low - 1; i >= 0; i--)
        {
            if (swings[i].Start < candle)
                return swings[i];
        }

        return null;
    }

    // The move a retracement is read against, from the previous swing's extreme to this one's start.
    private double Gap(bool up, int from, int to) => up ? Price(to, true) - Price(from, false) : Price(from, true) - Price(to, false);

    // Whether the move is large enough to read a retracement against: see Sawtooth.Retracement.
    private bool Readable(bool up, int from, int to)
    {
        var gap = Gap(up, from, to);
        return gap > 0 && gap >= Math.Abs(Price(from, !up)) * Sawtooth.MinimumRetracementMovePercent / 100;
    }

    // How far back along the move a candle's price is, as a whole percentage rounded down.
    private int Percent(bool up, int from, int to, int candle)
    {
        var back = up ? Price(to, true) - Price(candle, false) : Price(candle, true) - Price(to, false);
        return (int)Math.Floor(100 * back / Gap(up, from, to));
    }

    // Adds each new furthest point of the counter-move, from the candle after the last read up to a candle.
    private void Step(List<int> steps, bool up, int from, int to)
    {
        for (var c = from; c <= to; c++)
        {
            if (steps.Count == 0 || (up ? Price(c, false) < Price(steps[^1], false) : Price(c, true) > Price(steps[^1], true)))
                steps.Add(c);
        }
    }

    // A swing's retracement against a previous swing's extreme; none when the move is too small to read against.
    private ReadRetracement? Confirmed(Held swing, int from)
    {
        if (_confirmed.TryGetValue((swing.Read, from), out var retracement))
            return retracement;

        if (Readable(swing.Up, from, swing.Start) && swing.Start < swing.Extreme)
        {
            var steps = new List<int>();
            Step(steps, swing.Up, swing.Start + 1, swing.Extreme);
            retracement = new ReadRetracement(++_retracementIds, swing.Read.Direction, from, swing.Start, swing.Extreme, steps);
        }

        return _confirmed[(swing.Read, from)] = retracement;
    }

    // The candidate that starts at a candle, its counter-move read up to the last candle.
    private ReadCandidate Candidate(bool up, int start, int last)
    {
        if (!_open.TryGetValue((up, start), out var open))
        {
            var steps = new List<int>();
            _open[(up, start)] = open = new Open(new ReadCandidate(++_candidateIds, up ? EnumSwingDirection.Up : EnumSwingDirection.Down, start, steps), steps) { Read = start };
        }

        Step(open.Steps, up, open.Read + 1, last);
        open.Read = last;
        return open.Candidate;
    }

    // A candidate's retracement against a previous swing's extreme; none when the move is too small to read against.
    private ReadRetracement? Opened(ReadCandidate candidate, int from)
    {
        if (!Readable(candidate.Direction == EnumSwingDirection.Up, from, candidate.Start))
            return null;
        if (!_opened.TryGetValue((candidate.Id, from), out var retracement))
            _opened[(candidate.Id, from)] = retracement = new ReadRetracement(++_retracementIds, candidate.Direction, from, candidate.Start, null, candidate.Steps);
        return retracement;
    }

    // A level's ranges at the last candle: found again among the retracements that what changed can have changed, and the
    // one that holds read for its end.
    private void ReadRanges(Level level, int last)
    {
        var swings = level.Swings;
        var finer = level.Number < _levels.Count ? _levels[level.Number] : null;
        var from = Math.Min(level.Reread, level.Moved);
        if (finer != null && finer.BrokenFrom != int.MaxValue)
        {
            // A break one level finer identifies a range while the counter-move lasts: until the swing breaks structure,
            // and for a candidate until now.
            for (var i = swings.Count - 1; i >= 0 && swings[i].Break > finer.BrokenFrom; i--)
                from = Math.Min(from, swings[i].Start);
            foreach (var (candidate, _) in level.Candidates)
                from = Math.Min(from, candidate.Start);
        }

        if (from != int.MaxValue)
            Find(level, finer, from, last);

        if (level.Ranges.Count > 0 && level.Ranges[^1] is { End: < 0 } holds && Ended(holds, last))
            _ranges.Put(holds.Range, new RangeState(level.Number, holds.End));
    }

    // Finds the ranges again among the retracements that start at or after a candle, as Ranges.Find does among them all:
    // in the order they start, and at most one holding at a time. Those before stay as they are.
    private void Find(Level level, Level? finer, int from, int last)
    {
        var ranges = level.Ranges;
        var keep = ranges.Count;
        while (keep > 0 && ranges[keep - 1].To >= from)
            keep--;
        var were = ranges.GetRange(keep, ranges.Count - keep);
        ranges.RemoveRange(keep, ranges.Count - keep);

        // In the order the swings broke structure, the candidates after them, and then by where each starts.
        var read = new List<(ReadRetracement Retracement, int Limit)>();
        var swings = level.Swings;
        var first = swings.Count;
        while (first > 0 && swings[first - 1].Break > from)
            first--;
        for (var i = first; i < swings.Count; i++)
        {
            if (swings[i].Retracement is { } retracement && retracement.To >= from)
                read.Add((retracement, swings[i].Break));
        }

        foreach (var (_, retracement) in level.Candidates)
        {
            if (retracement != null && retracement.To >= from)
                read.Add((retracement, int.MaxValue));
        }

        foreach (var (retracement, limit) in read.OrderBy(x => x.Retracement.To))
        {
            if (Identify(level, finer, retracement, limit) is not { } found)
                continue;
            if (ranges.Count > 0)
            {
                Ended(ranges[^1], last);
                if (ranges[^1].End < 0 || ranges[^1].End >= found.Range.Identified)
                    continue;
            }

            Ended(found, last);
            ranges.Add(found);
        }

        foreach (var range in were)
            _ranges.Drop(range.Range);
        for (var i = keep; i < ranges.Count; i++)
            _ranges.Put(ranges[i].Range, new RangeState(level.Number, ranges[i].End < 0 ? null : ranges[i].End));
    }

    // The range a retracement makes, as Ranges.Find identifies one: the trend it comes out of holds where it starts, and
    // while the counter-move lasts a break one level finer, of a high or low made since, finds it at least the least
    // share of the way back and not the whole way.
    private Found? Identify(Level level, Level? finer, ReadRetracement retracement, int limit)
    {
        var up = retracement.Direction == EnumSwingDirection.Up;
        if (finer == null || !Holds(level, up, retracement.To))
            return null;

        var against = finer.Broken[up ? 1 : 0];
        var steps = retracement.Steps;
        for (var i = UpTo(against, retracement.To); i < against.Count && against[i].BeyondAt < limit; i++)
        {
            if (against[i].Extreme <= retracement.To)
                continue;

            // The furthest point of the counter-move by the break.
            var deepest = -1;
            for (var (low, high) = (0, steps.Count - 1); low <= high;)
            {
                var middle = (low + high) / 2;
                if (steps[middle] <= against[i].BeyondAt)
                    (deepest, low) = (steps[middle], middle + 1);
                else
                    high = middle - 1;
            }

            if (deepest < 0)
                continue;
            var percent = Percent(up, retracement.From, retracement.To, deepest);
            if (percent < minRetracement)
                continue;
            if (percent >= 100)
                return null;

            var (bottom, top) = up ? (deepest, retracement.To) : (retracement.To, deepest);
            var key = (retracement.Direction, retracement.From, bottom, top, against[i].BeyondAt, percent);
            if (!_found.TryGetValue(key, out var found))
            {
                var height = Price(top, true) - Price(bottom, false);
                _found[key] = found = new Found(new ReadRange(_found.Count + 1, retracement.Direction, retracement.From, bottom, top, against[i].BeyondAt, percent, band), retracement.To)
                {
                    BandLow = Price(bottom, false) - height * band / 100,
                    BandHigh = Price(top, true) + height * band / 100,
                };
            }

            return found;
        }

        return null;
    }

    // Whether a trend one way holds at a candle: confirmed by then and not ended by it. The trends follow one another, so
    // it is the last confirmed by then.
    private static bool Holds(Level level, bool up, int candle)
    {
        var heads = level.Heads;
        var (low, high) = (0, heads.Count);
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (heads[middle].Confirmed <= candle)
                low = middle + 1;
            else
                high = middle;
        }

        return low > 0 && heads[low - 1].Up == up && (heads[low - 1].End < 0 || heads[low - 1].End > candle);
    }

    // Reads a range on to a candle for the close beyond its band that ends it. Returns whether it has ended.
    private bool Ended(Found found, int last)
    {
        for (var c = Math.Max(found.Range.Identified, found.Read) + 1; found.End < 0 && c <= last; c++)
        {
            if (Price(c, true) > found.BandHigh || Price(c, false) < found.BandLow)
                found.End = c;
        }

        found.Read = Math.Max(found.Read, last);
        return found.End >= 0;
    }

    /// <summary>
    /// A confirmed swing with what has been worked out of it: the level it lies at (-1 when it is one no longer), the first
    /// close beyond the pivot against it and how far that has been looked for, whether that is a market structure break
    /// at its level, its retracement, and the trend it begins.
    /// </summary>
    private sealed class Held(ReadSwing read)
    {
        public ReadSwing Read => read;

        public bool Up { get; } = read.Direction == EnumSwingDirection.Up;

        public int Start => read.Start;

        public int Extreme => read.Extreme;

        public int Break => read.Break;

        public int Level { get; set; } = -1;

        public int BeyondAt { get; set; } = -1;

        public int BeyondRead { get; set; } = -1;

        public bool Broken { get; set; }

        public ReadBreak? Item { get; set; }

        public ReadRetracement? Retracement { get; set; }

        public ReadTrend? Trend { get; set; }

        public bool Heads { get; set; }

        public int Confirmed { get; set; }

        public int End { get; set; } = -1;
    }

    /// <summary>
    /// The swings sharing a level, in the order they broke structure, and what is read of them: those whose pivot is broken,
    /// by the way of the swing (upswings first) and in the order of the closes beyond; the swings that begin its trends;
    /// the last swing each way; its ranges in the order their retracements start; and its candidates with their
    /// retracements. The rest is what the candle being read has changed: the first place among the swings, the earliest
    /// start of a swing that came or went or was read again, the earliest close beyond a pivot that became a break or
    /// stopped being one, and the candle from which the ranges are to be found again.
    /// </summary>
    private sealed class Level(int number)
    {
        public int Number => number;

        public List<Held> Swings { get; } = [];

        public List<Held>[] Broken { get; } = [[], []];

        public List<Held> Heads { get; } = [];

        public Held?[] Last { get; } = new Held?[2];

        public List<Found> Ranges { get; } = [];

        public List<(ReadCandidate Candidate, ReadRetracement? Retracement)> Candidates { get; set; } = [];

        public int Dirty { get; set; } = int.MaxValue;

        public int Moved { get; set; } = int.MaxValue;

        public int BrokenFrom { get; set; } = int.MaxValue;

        public int Reread { get; set; } = int.MaxValue;
    }

    // A candidate with the steps of its counter-move, which are added to, and the last candle they were read to.
    private sealed class Open(ReadCandidate candidate, List<int> steps)
    {
        public ReadCandidate Candidate => candidate;

        public List<int> Steps => steps;

        public int Read { get; set; }
    }

    // A range with the candle its retracement starts at, its band, and its end: the candle (-1 while it holds) and how far
    // that has been looked for.
    private sealed class Found(ReadRange range, int to)
    {
        public ReadRange Range => range;

        public int To => to;

        public double BandLow { get; init; }

        public double BandHigh { get; init; }

        public int End { get; set; } = -1;

        public int Read { get; set; } = -1;
    }

    /// <summary>
    /// The items of one kind that hold, each with its state, and what a candle changes of them. Through a candle an item is
    /// put with a state or dropped, and at its end one put anywhere holds, whatever dropped it: so an item that leaves one
    /// level for another is the same item at its new level.
    /// </summary>
    private sealed class Kind<TItem, TState> where TItem : class where TState : struct, IEquatable<TState>
    {
        private readonly Dictionary<TItem, TState> _holds = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<TItem, TState> _put = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<TItem> _dropped = new(ReferenceEqualityComparer.Instance);

        public void Put(TItem item, TState state) => _put[item] = state;

        public void Drop(TItem item) => _dropped.Add(item);

        /// <summary>
        /// Ends the candle: what it changed, in the order of the items' ids.
        /// </summary>
        public List<(TItem Item, TState? State)> Take(Func<TItem, long> id)
        {
            var changes = new List<(TItem Item, TState? State)>();
            foreach (var item in _dropped)
            {
                if (!_put.ContainsKey(item) && _holds.Remove(item))
                    changes.Add((item, null));
            }

            foreach (var (item, state) in _put)
            {
                if (_holds.TryGetValue(item, out var was) && was.Equals(state))
                    continue;
                _holds[item] = state;
                changes.Add((item, state));
            }

            _put.Clear();
            _dropped.Clear();
            changes.Sort((a, b) => id(a.Item).CompareTo(id(b.Item)));
            return changes;
        }
    }
}
