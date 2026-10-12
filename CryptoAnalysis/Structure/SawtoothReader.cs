namespace Gradient.CryptoAnalysis;

/// <summary>
/// Reads the sawtooth a candle at a time, and after each candle has what <see cref="Sawtooth"/> would make of the candles
/// so far (its levels, swings, points, candidates and retracements) without reading them all again.
/// A leg between two pivots that can no longer move is read once and kept: what is inside it does not change, only how deep
/// it lies. What a new candle can change is the last leg of each level, the one still running to the current price, and
/// each of those is followed a candle at a time. So a candle costs a step for each level, and the reading of a leg when it
/// is settled, whatever the number of candles before it.
/// A leg is known by the candles of its two ends, and a level is how far down a leg lies, so nothing here is kept by level.
/// </summary>
public sealed class SawtoothReader(EnumPriceBasis basis)
{
    private readonly List<Price> _prices = [];
    private readonly Dictionary<(int From, bool Start, int To), Leg> _settled = [];
    private Dictionary<(int From, bool Up), Run> _running = [];

    // The legs running to the current price, level 0's first: each begins at the last pivot of the one before.
    private List<Run> _spine = [];
    private Run? _first;
    private int _high;
    private bool _fromStart;

    // The pivots and swings the structure has, each with the level it lies at, and what the last candle changed of them.
    private readonly Dictionary<(int Candle, EnumPivotKind Kind), (int Level, int Confirmed)> _pivots = [];
    private readonly Dictionary<ReadSwing, int> _swings = [];
    private readonly Dictionary<(int Candle, EnumPivotKind Kind), (int Level, int Confirmed)?> _pivotsBefore = [];
    private readonly Dictionary<ReadSwing, int?> _swingsBefore = [];

    private EnumPriceBasis Basis => basis;

    /// <summary>
    /// The pivots that can no longer move, by their candle and kind (with wicks a candle can be a high and a low), each
    /// with the level it lies at now and the candle it could no longer move from.
    /// </summary>
    public IReadOnlyDictionary<(int Candle, EnumPivotKind Kind), (int Level, int Confirmed)> PivotLevels => _pivots;

    /// <summary>
    /// The confirmed swings, each with the level it lies at now.
    /// </summary>
    public IReadOnlyDictionary<ReadSwing, int> SwingLevels => _swings;

    /// <summary>
    /// What the last candle read changed: each pivot that became one or changed level (no level when it is one no
    /// longer) and each swing that was confirmed or changed level (no level when it is one no longer). Only the first
    /// higher high than the first candle's can unmake any: the first candle is then the start, not a high, and on wicks
    /// what was read under its high is read again from its close. A candle that changes nothing gives none.
    /// </summary>
    public (IReadOnlyList<PivotChange> Pivots, IReadOnlyList<SwingChange> Swings) Changes { get; private set; } = ([], []);

    /// <summary>
    /// How many candles have been read.
    /// </summary>
    public int Count => _prices.Count;

    /// <summary>
    /// Reads the next candle.
    /// </summary>
    public void Add(Price price)
    {
        _prices.Add(price);
        _pivotsBefore.Clear();
        _swingsBefore.Clear();
        Changes = ([], []);
        var last = _prices.Count - 1;
        if (last == 0)
            return;

        if (Sawtooth.HighOf(price, basis) > Sawtooth.HighOf(_prices[_high], basis))
            _high = last;
        _first ??= new Run(this, Start, up: true);
        _first.Advance(last);
        if (_high == 0)
        {
            // The first candle is the high so far: a pivot of level 0, and the leg from the first price is not yet one.
            Place(0, EnumPivotKind.High, 0, 1);
        }
        else
        {
            // The leg from the first price to the highest high: its pivots are level 1's, but for the highest high itself,
            // and while that is on the last candle the leg up to it from the pivot before is read as it stands.
            if (!_fromStart)
                ReadFromStart();
            _first.Place(1);
            var seen = _first.Seen;
            if (seen.Count > 0 && seen[^1].Index != _high)
                Place(Settled(seen[^1], High), 1);
            foreach (var pivot in seen.Skip(seen.Count - 2))
                Place(pivot, pivot.Index == _high ? 0 : 1);
        }

        var running = new Dictionary<(int, bool), Run>();
        var spine = new List<Run>();
        for (var from = HasCurrent ? High : null; from != null;)
        {
            var key = (from.Index, from.Kind == EnumPivotKind.Low);
            if (!_running.TryGetValue(key, out var run))
                run = new Run(this, from, key.Item2);
            run.Place(spine.Count + 1);
            run.Advance(last);
            running[key] = run;
            spine.Add(run);
            from = run.Seen.Count > 0 ? run.Seen[^1] : null;
        }

        _running = running;
        _spine = spine;
        Changes = (
            _pivotsBefore.Select(x => (x.Key, Was: x.Value, Now: _pivots.TryGetValue(x.Key, out var now) ? now : ((int Level, int Confirmed)?)null)).Where(x => x.Was != x.Now).OrderBy(x => x.Key)
                .Select(x => new PivotChange(x.Key.Candle, x.Key.Kind, Extreme(x.Key.Candle, x.Key.Kind == EnumPivotKind.High), x.Now?.Level, x.Now?.Confirmed)).ToList(),
            _swingsBefore.Select(x => (x.Key, Was: x.Value, Now: _swings.TryGetValue(x.Key, out var now) ? now : (int?)null)).Where(x => x.Was != x.Now)
                .OrderBy(x => x.Key.Break).ThenBy(x => x.Key.Start)
                .Select(x => new SwingChange(x.Key, x.Now)).ToList());
    }

    // A pivot lies at a level, and could no longer move from a candle: noted when either is new, with what they were
    // before this candle.
    private void Place(int index, EnumPivotKind kind, int level, int confirmed)
    {
        var had = _pivots.TryGetValue((index, kind), out var was);
        if (had && was == (level, confirmed))
            return;
        _pivotsBefore.TryAdd((index, kind), had ? was : null);
        _pivots[(index, kind)] = (level, confirmed);
    }

    private void Place(SawtoothPivot pivot, int level) => Place(pivot.Index, pivot.Kind, level, pivot.ConfirmedIndex!.Value);

    // The first candle was the high and is so no longer: it is the start, and all there was is read again from it. On
    // closes that leaves everything but the first candle's own pivot as it was; on wicks it need not.
    private void ReadFromStart()
    {
        _fromStart = true;
        foreach (var (pivot, level) in _pivots)
            _pivotsBefore.TryAdd(pivot, level);
        foreach (var (swing, level) in _swings)
            _swingsBefore.TryAdd(swing, level);
        _pivots.Clear();
        _swings.Clear();
        foreach (var leg in _settled.Values)
            leg.Level = -1;
        foreach (var run in _running.Values)
            run.Displace();
    }

    // A swing of the leg that begins at a candle lies at a level.
    private void Place(Swing swing, int leg, int level)
    {
        var read = new ReadSwing(Direction(swing.Up), swing.Start.Index, swing.Counter.Index, swing.Break, leg);
        var had = _swings.TryGetValue(read, out var was);
        if (had && was == level)
            return;
        _swingsBefore.TryAdd(read, had ? was : null);
        _swings[read] = level;
    }

    // A settled leg lies between two pivots of a level: the pivots inside it and its swings are of the level after, and
    // so are the legs between them. A leg already there is left, with all inside it.
    private void Place(Leg leg, int level)
    {
        if (leg.Inside.Count == 0 || leg.Level == level)
            return;
        leg.Level = level;
        foreach (var pivot in leg.Inside)
            Place(pivot, level + 1);
        foreach (var swing in leg.Swings)
            Place(swing, leg.From, level + 1);
        foreach (var inner in leg.Legs)
            Place(inner, level + 1);
    }

    /// <summary>
    /// Levels 0 to maxLevel of the candles read so far, stopping early once a level adds no new pivots: the same as
    /// <see cref="Sawtooth.Levels"/> makes of them.
    /// </summary>
    public List<SawtoothLevel> Levels(int maxLevel = 10)
    {
        if (_prices.Count == 0)
            return [];

        var (pivots, legs) = LevelZero();
        var levels = new List<SawtoothLevel> { new(0, pivots) };
        while (levels.Count <= maxLevel)
        {
            var (nextPivots, nextLegs) = Refine(pivots, legs);
            if (nextPivots.Count == pivots.Count)
                break;
            levels.Add(new SawtoothLevel(levels.Count, nextPivots));
            (pivots, legs) = (nextPivots, nextLegs);
        }

        return levels;
    }

    /// <summary>
    /// The confirmed swings at a level (1 or above) of the candles read so far: the same as <see cref="Sawtooth.Swings"/>
    /// makes of them.
    /// </summary>
    public List<SwingOutline> Swings(int level) => SwingsAt(level).Select(swing => Outline(swing, level)).ToList();

    // A swing as the sawtooth gives it at a level, made once for the level it lies at.
    internal SwingOutline Outline(Swing swing, int level)
    {
        if (swing.Outline?.Level == level)
            return swing.Outline;
        var broke = Point(swing.Break, swing.Up);
        return swing.Outline = new SwingOutline(level, Direction(swing.Up), Point(swing.Start), broke, Point(swing.Counter), broke);
    }

    /// <summary>
    /// The candles read so far.
    /// </summary>
    internal List<Price> Read => _prices;

    /// <summary>
    /// What a replay needs of a level at the last candle: the end of its pivots, from a few before the first at or after a
    /// candle (all of them when that is the first candle), the candle of the furthest point of its leg still running, when
    /// it has one, and every swing of the level in the order they broke structure.
    /// </summary>
    internal sealed record LevelRead(List<SawtoothPivot> Pivots, int? Furthest, List<Swing> Swings);

    /// <summary>
    /// Levels 0 to maxLevel as <see cref="Levels"/> gives them, each as a replay needs it (see <see cref="LevelRead"/>):
    /// the legs of every level are gone through, which is quick, but only the end of each level's pivots is listed.
    /// </summary>
    internal List<LevelRead> ReadLevels(int maxLevel, int from)
    {
        var read = new List<LevelRead>();
        if (_prices.Count == 0)
            return read;

        // Enough pivots before the first to label it against the one before it of its kind.
        const int before = 8;
        var (pivots, legs) = LevelZero();
        var all = legs;
        var swings = new List<Swing>();
        while (true)
        {
            int? furthest = Sawtooth.EndsInAnIncompleteLeg(pivots) && legs[^1].Following is { Furthest: >= 0 } run ? run.Furthest : null;
            read.Add(new LevelRead(pivots, furthest, swings));
            if (read.Count > maxLevel || !all.Any(x => x.Inside.Count > 0))
                break;

            swings = all.SelectMany(x => x.Swings).ToList();
            var next = new List<Leg>(all.Count);
            foreach (var leg in all)
            {
                if (leg.Inside.Count > 0)
                    next.AddRange(leg.Legs);
                else
                    next.Add(leg);
            }

            all = next;

            // The legs that end at or after the candle, and a few before them, with all inside them.
            var first = from <= 0 ? 0 : Math.Max(0, Enumerable.Range(0, legs.Count).FirstOrDefault(i => pivots[i + 1].Index >= from, legs.Count - 1) - before);
            (pivots, legs) = Refine(pivots.GetRange(first, pivots.Count - first), legs.GetRange(first, legs.Count - first));
        }

        return read;
    }

    /// <summary>
    /// A level's highs and lows as currently known, labelled against the previous one of their kind: the same as
    /// <see cref="Sawtooth.Points(List{Price}, SawtoothLevel, EnumPriceBasis)"/> makes of that level. A level the candles do
    /// not have has none.
    /// </summary>
    public List<StructurePoint> Points(int level)
    {
        if (level < 0 || _prices.Count == 0)
            return [];

        var (pivots, legs) = LevelZero();
        for (var l = 0; l < level; l++)
        {
            var (nextPivots, nextLegs) = Refine(pivots, legs);
            if (nextPivots.Count == pivots.Count)
                return [];
            (pivots, legs) = (nextPivots, nextLegs);
        }

        // The furthest point of the leg still running is kept as the leg is followed.
        int? furthest = Sawtooth.EndsInAnIncompleteLeg(pivots) && legs[^1].Following is { Furthest: >= 0 } run ? run.Furthest : null;
        return Sawtooth.Points(_prices, new SawtoothLevel(level, pivots), basis, furthest);
    }

    /// <summary>
    /// The candidate swings at a level (1 or above) of the candles read so far: the same as
    /// <see cref="Sawtooth.Candidates"/> makes of them.
    /// </summary>
    public List<CandidateSwing> Candidates(int level) => CandidatesAt(level)
        .Select(x => new CandidateSwing(level, Direction(x.Up), Point(x.Start), Point(x.Counter, !x.Up)))
        .ToList();

    /// <summary>
    /// The retracements at a level (1 or above) of the candles read so far: the same as
    /// <see cref="Sawtooth.Retracements"/> makes of that level's swings and candidates.
    /// </summary>
    public List<RetracementOutline> Retracements(int level)
    {
        // In the order they broke structure, which is the order they are met in.
        return Retracements(level, SwingsAt(level).OrderBy(x => x.Break).ToList());
    }

    // The retracements of a level's swings, given in the order they broke structure, and of its candidates. A swing's is
    // kept with it, and made again only when the swing before it or its level has changed.
    internal List<RetracementOutline> Retracements(int level, List<Swing> swings)
    {
        var retracements = new List<RetracementOutline>();
        for (var i = 0; i < swings.Count; i++)
        {
            var swing = swings[i];
            var previous = Before(swing.Start, i);
            if (!swing.Retraced || swing.RetracedFrom != previous || swing.RetracedLevel != level)
            {
                swing.Retracement = Read(previous, swing.Up, swing.Start, swing.Counter.Index, final: true);
                (swing.Retraced, swing.RetracedFrom, swing.RetracedLevel) = (true, previous, level);
            }

            if (swing.Retracement != null)
                retracements.Add(swing.Retracement);
        }

        foreach (var candidate in CandidatesAt(level))
        {
            if (Read(Before(candidate.Start, swings.Count), candidate.Up, candidate.Start, _prices.Count - 1, final: false) is { } retracement)
                retracements.Add(retracement);
        }

        return retracements.OrderBy(x => x.To.Time).ToList();

        // The last swing to break structure before a pivot's candle that also starts before it: nearly always the one
        // just before.
        Swing? Before(SawtoothPivot to, int before)
        {
            for (var i = before - 1; i >= 0; i--)
            {
                if (swings[i].Break <= to.Index && swings[i].Start.Index < to.Index)
                    return swings[i];
            }

            return null;
        }

        RetracementOutline? Read(Swing? previous, bool up, SawtoothPivot to, int end, bool final) => previous != null && previous.Up == up
            ? Sawtooth.Retracement(_prices, basis, level, Direction(up), Point(previous.Counter), Point(to), to.Index, end, final)
            : null;
    }

    private SawtoothPivot Start => Sawtooth.Pivot(EnumPivotKind.Start, _prices, 0, basis) with { ConfirmedIndex = 0 };

    private SawtoothPivot High => Sawtooth.Pivot(EnumPivotKind.High, _prices, _high, basis) with { ConfirmedIndex = _high < _prices.Count - 1 ? _high + 1 : null };

    private SawtoothPivot Current => Sawtooth.Pivot(EnumPivotKind.Current, _prices, _prices.Count - 1, basis);

    // Level 0 ends at the current price unless that is the highest high itself.
    private bool HasCurrent => !(_high == _prices.Count - 1 && Current.Price == High.Price);

    private static EnumSwingDirection Direction(bool up) => up ? EnumSwingDirection.Up : EnumSwingDirection.Down;

    private static PricePoint Point(SawtoothPivot pivot) => new(pivot.Time, pivot.Price);

    private PricePoint Point(int index, bool high) => new(_prices[index].DateTime, Extreme(index, high));

    // A candle's high or low, as the basis has it.
    internal double Extreme(int index, bool high) => high ? Sawtooth.HighOf(_prices[index], basis) : Sawtooth.LowOf(_prices[index], basis);

    // Level 0: the first price, the highest high and the current price, and the legs between them.
    private (List<SawtoothPivot> Pivots, List<Leg> Legs) LevelZero()
    {
        var pivots = new List<SawtoothPivot>();
        var legs = new List<Leg>();
        var high = High;
        if (_high != 0)
        {
            pivots.Add(Start);
            // The leg up to the highest high is followed from the first price: its last high so far is the highest high.
            var seen = _first!.Seen;
            if (seen.Count > 0 && seen[^1].Index == _high)
                legs.Add(new Leg(seen.Take(seen.Count - 1).ToList(), seen.Count > 1 ? _first.Legs : [], _first.Swings));
            else if (seen.Count > 0)
                legs.Add(new Leg(seen, [.. _first.Legs, Settled(seen[^1], high)], _first.Swings));
            else
                legs.Add(Leg.None);
        }

        pivots.Add(high);
        if (HasCurrent)
        {
            pivots.Add(Current);
            legs.Add(Running(high));
        }

        return (pivots, legs);
    }

    // The next level: each leg's pivots between its ends, and the legs between those; a leg with nothing in it stays.
    private static (List<SawtoothPivot> Pivots, List<Leg> Legs) Refine(List<SawtoothPivot> pivots, List<Leg> legs)
    {
        var nextPivots = new List<SawtoothPivot> { pivots[0] };
        var nextLegs = new List<Leg>();
        for (var i = 0; i < legs.Count; i++)
        {
            nextPivots.AddRange(legs[i].Inside);
            nextPivots.Add(pivots[i + 1]);
            if (legs[i].Inside.Count > 0)
                nextLegs.AddRange(legs[i].Legs);
            else
                nextLegs.Add(legs[i]);
        }

        return (nextPivots, nextLegs);
    }

    // A level's swings by their candles: a swing lies inside a leg of the level above.
    private List<Swing> SwingsAt(int level)
    {
        if (level < 1 || _prices.Count == 0)
            return [];

        var (pivots, legs) = LevelZero();
        for (var l = 1; l < level; l++)
            (pivots, legs) = Refine(pivots, legs);
        return legs.SelectMany(x => x.Swings).ToList();
    }

    /// <summary>
    /// How many legs are running to the current price: a level from this on has no candidate.
    /// </summary>
    internal int Depth => _spine.Count;

    // A level's candidates by their candles: where each starts, and the candle of the furthest price against it since.
    internal List<(bool Up, SawtoothPivot Start, int Counter)> CandidatesAt(int level)
    {
        var candidates = new List<(bool, SawtoothPivot, int)>();
        if (level < 1 || _spine.Count == 0)
            return candidates;

        // The series high with the pullback since, which the leg running from it follows.
        if (level == 1 && _high != 0 && _spine[0].Furthest >= 0)
            candidates.Add((true, High, _spine[0].Furthest));

        // The last extreme of the level above's running leg, when price has turned from it, with the counter-move since:
        // the leg running from it at this level follows that.
        if (level < _spine.Count && _spine[level - 1] is { Seen.Count: > 0 } run && run.Seen[^1].Kind == (run.Up ? EnumPivotKind.High : EnumPivotKind.Low)
            && _spine[level].Furthest >= 0)
            candidates.Add((run.Up, run.Seen[^1], _spine[level].Furthest));

        return candidates;
    }

    // The leg running from a pivot to the current price, as it is being followed.
    private Leg Running(SawtoothPivot from)
    {
        if (!_running.TryGetValue((from.Index, from.Kind == EnumPivotKind.Low), out var run))
            return Leg.None;
        return run.Seen.Count == 0
            ? new Leg([], [], run.Swings) { Following = run }
            : new Leg(run.Seen, [.. run.Legs, Running(run.Seen[^1])], run.Swings) { Following = run };
    }

    // The leg between two pivots that can no longer move, read once and kept.
    private Leg Settled(SawtoothPivot from, SawtoothPivot to)
    {
        var key = (from.Index, from.Kind == EnumPivotKind.Start, to.Index);
        if (_settled.TryGetValue(key, out var leg))
            return leg;

        var up = to.Kind == EnumPivotKind.High;
        var inside = Sawtooth.Chain(_prices, basis, from, to.Index, up, includeLastExtreme: false);
        if (inside.Count == 0)
            return _settled[key] = Leg.None;

        var ends = new List<SawtoothPivot> { from };
        ends.AddRange(inside);
        ends.Add(to);
        var legs = new List<Leg>();
        for (var i = 0; i + 1 < ends.Count; i++)
            legs.Add(Settled(ends[i], ends[i + 1]));

        // Its swings, as Sawtooth.Swings finds them in a leg that ends at a pivot.
        var swings = new List<Swing>();
        var turns = ends.Where(x => x.Kind is EnumPivotKind.High or EnumPivotKind.Low).ToList();
        for (var t = 0; t + 2 < turns.Count; t++)
        {
            if (turns[t].Kind != (up ? EnumPivotKind.High : EnumPivotKind.Low))
                continue;
            for (var c = turns[t + 1].Index + 1; c <= turns[t + 2].Index; c++)
            {
                var value = Extreme(c, up);
                if (up ? value > turns[t].Price : value < turns[t].Price)
                {
                    swings.Add(new Swing(up, turns[t], turns[t + 1], c));
                    break;
                }
            }
        }

        return _settled[key] = new Leg(inside, legs, swings) { From = from.Index };
    }

    /// <summary>
    /// A swing by its candles: the pivot it begins at, the pivot against it, and the candle that breaks structure. What has
    /// been worked out of it is kept with it, to be used again while it holds: its outline at a level, the first close
    /// beyond the pivot against it (a market structure break when it comes while the swing still protects that pivot) with
    /// how far that has been looked for, and its retracement against the swing before it at a level.
    /// </summary>
    internal sealed class Swing(bool up, SawtoothPivot start, SawtoothPivot counter, int @break)
    {
        public bool Up => up;

        public SawtoothPivot Start => start;

        public SawtoothPivot Counter => counter;

        public int Break => @break;

        public SwingOutline? Outline { get; set; }

        public int BeyondAt { get; set; } = -1;

        public int BeyondRead { get; set; } = -1;

        public MarketStructureBreakOutline? Beyond { get; set; }

        public bool Retraced { get; set; }

        public Swing? RetracedFrom { get; set; }

        public int RetracedLevel { get; set; }

        public RetracementOutline? Retracement { get; set; }
    }

    /// <summary>
    /// What is inside a leg: the pivots between its ends, the legs between those (one more than the pivots), and the swings
    /// those pivots make. Following is how a leg still running to the current price is being followed.
    /// </summary>
    private sealed record Leg(IReadOnlyList<SawtoothPivot> Inside, IReadOnlyList<Leg> Legs, IReadOnlyList<Swing> Swings)
    {
        public static readonly Leg None = new([], [], []);

        public Run? Following { get; init; }

        /// <summary>
        /// The candle a settled leg begins at.
        /// </summary>
        public int From { get; init; }

        /// <summary>
        /// The level a settled leg was last placed at; -1 before it is placed.
        /// </summary>
        public int Level { get; set; } = -1;
    }

    /// <summary>
    /// A leg being followed from a pivot towards the current price, a candle at a time: as <see cref="Sawtooth.Chain"/>
    /// reads it, the end of each run of new extremes and the furthest price against the leg before the next run.
    /// </summary>
    private sealed class Run(SawtoothReader reader, SawtoothPivot from, bool up)
    {
        private int _at = from.Index;
        private double _best = from.Kind == EnumPivotKind.Start ? from.Price : reader.Extreme(from.Index, up);
        private bool _inRun;
        private int _runs;
        private int _end;

        public bool Up => up;

        /// <summary>
        /// The level of the leg's pivots, once it is part of the structure; -1 before.
        /// </summary>
        public int Level { get; private set; } = -1;

        /// <summary>
        /// Forgets where the leg was placed, so that it is placed afresh.
        /// </summary>
        public void Displace() => Level = -1;

        /// <summary>
        /// Says the level the leg's pivots lie at: they, its swings and the settled legs between them are placed there.
        /// </summary>
        public void Place(int level)
        {
            if (Level == level)
                return;
            Level = level;
            foreach (var pivot in Seen)
                reader.Place(pivot, level);
            foreach (var swing in Swings)
                reader.Place(swing, from.Index, level);
            foreach (var leg in Legs)
                reader.Place(leg, level);
        }

        /// <summary>
        /// The leg's pivots so far, but for an extreme on the last candle, which is still running.
        /// </summary>
        public List<SawtoothPivot> Seen { get; } = [];

        /// <summary>
        /// The settled legs between the pivots seen, the first from where the leg begins.
        /// </summary>
        public List<Leg> Legs { get; } = [];

        public List<Swing> Swings { get; } = [];

        /// <summary>
        /// The candle of the furthest price the leg's way since it began, the first to reach it; -1 before its first candle.
        /// This is the leg's point while it runs, and the counter-move of a candidate that starts where the leg does.
        /// </summary>
        public int Furthest { get; private set; } = -1;

        /// <summary>
        /// Follows the leg on to a candle.
        /// </summary>
        public void Advance(int last)
        {
            while (_at < last)
                Step(++_at);
        }

        private void Step(int i)
        {
            var value = reader.Extreme(i, up);
            if (Furthest < 0 || (up ? value > reader.Extreme(Furthest, true) : value < reader.Extreme(Furthest, false)))
                Furthest = i;

            if (!(up ? value > _best : value < _best))
            {
                // The run has ended: its extreme is a pivot, though a later run may still pass it.
                if (_inRun)
                    See(Pivot(up, _end, _end + 1));
                _inRun = false;
                return;
            }

            _best = value;
            if (!_inRun)
            {
                // A new run: the furthest price against the leg since the last run, or since the start, is a pivot, and
                // with the extreme before it a swing, which this candle confirms.
                var first = _runs == 0 ? from.Index + 1 : _end + 1;
                if (_runs > 0 || (from.Kind == EnumPivotKind.Start && i > first))
                {
                    var counter = first;
                    for (var c = first + 1; c < i; c++)
                    {
                        if (up ? reader.Extreme(c, false) < reader.Extreme(counter, false) : reader.Extreme(c, true) > reader.Extreme(counter, true))
                            counter = c;
                    }

                    See(Pivot(!up, counter, i));
                    if (_runs > 0)
                    {
                        Swings.Add(new Swing(up, Seen[^2], Seen[^1], i));
                        if (Level >= 0)
                            reader.Place(Swings[^1], from.Index, Level);
                    }
                }

                _runs++;
            }

            _end = i;
            _inRun = true;
        }

        private SawtoothPivot Pivot(bool high, int index, int confirmed) =>
            Sawtooth.Pivot(high ? EnumPivotKind.High : EnumPivotKind.Low, reader._prices, index, reader.Basis) with { ConfirmedIndex = confirmed };

        private void See(SawtoothPivot pivot)
        {
            Legs.Add(reader.Settled(Seen.Count > 0 ? Seen[^1] : from, pivot));
            Seen.Add(pivot);
            if (Level >= 0)
            {
                reader.Place(pivot, Level);
                reader.Place(Legs[^1], Level);
            }
        }
    }
}

/// <summary>
/// A confirmed swing by its candles: the candle of the pivot it starts at, of the pivot against it, and of the break of
/// structure that confirms it. Leg is the candle the leg it lies inside begins at.
/// </summary>
public sealed record ReadSwing(EnumSwingDirection Direction, int Start, int Extreme, int Break, int Leg);

/// <summary>
/// A pivot that became one or changed on a candle: its candle, kind and price, the level it lies at now and the candle it
/// could no longer move from, as the leg it is now read in has it; neither when it is a pivot no longer.
/// </summary>
public sealed record PivotChange(int Candle, EnumPivotKind Kind, double Price, int? Level, int? Confirmed);

/// <summary>
/// A swing that was confirmed or changed level on a candle, with the level it lies at now, or no level when it is a
/// swing no longer.
/// </summary>
public sealed record SwingChange(ReadSwing Swing, int? Level);