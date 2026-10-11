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

    private EnumPriceBasis Basis => basis;

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
        var last = _prices.Count - 1;
        if (last == 0)
            return;

        if (Sawtooth.HighOf(price, basis) > Sawtooth.HighOf(_prices[_high], basis))
            _high = last;
        _first ??= new Run(this, Start, up: true);
        _first.Advance(last);

        var running = new Dictionary<(int, bool), Run>();
        var spine = new List<Run>();
        for (var from = HasCurrent ? High : null; from != null;)
        {
            var key = (from.Index, from.Kind == EnumPivotKind.Low);
            if (!_running.TryGetValue(key, out var run))
                run = new Run(this, from, key.Item2);
            run.Advance(last);
            running[key] = run;
            spine.Add(run);
            from = run.Seen.Count > 0 ? run.Seen[^1] : null;
        }

        _running = running;
        _spine = spine;
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
    public List<SwingOutline> Swings(int level) => SwingsAt(level).Select(swing =>
    {
        var broke = Point(swing.Break, swing.Up);
        return new SwingOutline(level, Direction(swing.Up), Point(swing.Start), broke, Point(swing.Counter), broke);
    }).ToList();

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
        var swings = SwingsAt(level).OrderBy(x => x.Break).ToList();
        var retracements = new List<RetracementOutline>();
        for (var i = 0; i < swings.Count; i++)
            Add(swings[i].Up, swings[i].Start, swings[i].Counter.Index, final: true, before: i);
        foreach (var candidate in CandidatesAt(level))
            Add(candidate.Up, candidate.Start, _prices.Count - 1, final: false, before: swings.Count);

        return retracements.OrderBy(x => x.To.Time).ToList();

        void Add(bool up, SawtoothPivot to, int end, bool final, int before)
        {
            // The last swing to break structure before this one starts: nearly always the one just before it.
            for (var i = before - 1; i >= 0; i--)
            {
                var previous = swings[i];
                if (previous.Break > to.Index || previous.Start.Index >= to.Index)
                    continue;
                if (previous.Up == up && Sawtooth.Retracement(_prices, basis, level, Direction(up), Point(previous.Counter), Point(to), to.Index, end, final) is { } retracement)
                    retracements.Add(retracement);
                return;
            }
        }
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
    private double Extreme(int index, bool high) => high ? Sawtooth.HighOf(_prices[index], basis) : Sawtooth.LowOf(_prices[index], basis);

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

    // A level's candidates by their candles: where each starts, and the candle of the furthest price against it since.
    private List<(bool Up, SawtoothPivot Start, int Counter)> CandidatesAt(int level)
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

        return _settled[key] = new Leg(inside, legs, swings);
    }

    /// <summary>
    /// A swing by its candles: the pivot it begins at, the pivot against it, and the candle that breaks structure.
    /// </summary>
    private sealed record Swing(bool Up, SawtoothPivot Start, SawtoothPivot Counter, int Break);

    /// <summary>
    /// What is inside a leg: the pivots between its ends, the legs between those (one more than the pivots), and the swings
    /// those pivots make. Following is how a leg still running to the current price is being followed.
    /// </summary>
    private sealed record Leg(IReadOnlyList<SawtoothPivot> Inside, IReadOnlyList<Leg> Legs, IReadOnlyList<Swing> Swings)
    {
        public static readonly Leg None = new([], [], []);

        public Run? Following { get; init; }
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
                        Swings.Add(new Swing(up, Seen[^2], Seen[^1], i));
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
        }
    }
}
