namespace Gradient.CryptoAnalysis;

/// <summary>
/// Reads the sawtooth a candle at a time, and after each candle has what <see cref="Sawtooth.Levels"/> and
/// <see cref="Sawtooth.Swings"/> would make of the candles so far, without reading them all again.
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

        // The leg running to the current price at each level: each begins at the last pivot of the one above.
        var running = new Dictionary<(int, bool), Run>();
        for (var from = HasCurrent ? High : null; from != null;)
        {
            var key = (from.Index, from.Kind == EnumPivotKind.Low);
            if (!_running.TryGetValue(key, out var run))
                run = new Run(this, from, key.Item2);
            run.Advance(last);
            running[key] = run;
            from = run.Seen.Count > 0 ? run.Seen[^1] : null;
        }

        _running = running;
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
    public List<SwingOutline> Swings(int level)
    {
        var swings = new List<SwingOutline>();
        if (level < 1 || _prices.Count == 0)
            return swings;

        // A swing lies inside a leg of the level above.
        var (pivots, legs) = LevelZero();
        for (var l = 1; l < level; l++)
            (pivots, legs) = Refine(pivots, legs);

        foreach (var swing in legs.SelectMany(x => x.Swings))
        {
            var candle = _prices[swing.Break];
            var broke = new PricePoint(candle.DateTime, swing.Up ? Sawtooth.HighOf(candle, basis) : Sawtooth.LowOf(candle, basis));
            swings.Add(new SwingOutline(level, swing.Up ? EnumSwingDirection.Up : EnumSwingDirection.Down, new PricePoint(swing.Start.Time, swing.Start.Price),
                broke, new PricePoint(swing.Counter.Time, swing.Counter.Price), broke));
        }

        return swings;
    }

    private SawtoothPivot Start => Sawtooth.Pivot(EnumPivotKind.Start, _prices, 0, basis) with { ConfirmedIndex = 0 };

    private SawtoothPivot High => Sawtooth.Pivot(EnumPivotKind.High, _prices, _high, basis) with { ConfirmedIndex = _high < _prices.Count - 1 ? _high + 1 : null };

    private SawtoothPivot Current => Sawtooth.Pivot(EnumPivotKind.Current, _prices, _prices.Count - 1, basis);

    // Level 0 ends at the current price unless that is the highest high itself.
    private bool HasCurrent => !(_high == _prices.Count - 1 && Current.Price == High.Price);

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

    // The leg running from a pivot to the current price, as it is being followed.
    private Leg Running(SawtoothPivot from)
    {
        if (!_running.TryGetValue((from.Index, from.Kind == EnumPivotKind.Low), out var run) || run.Seen.Count == 0)
            return run == null ? Leg.None : new Leg([], [], run.Swings);
        return new Leg(run.Seen, [.. run.Legs, Running(run.Seen[^1])], run.Swings);
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
                var value = up ? Sawtooth.HighOf(_prices[c], basis) : Sawtooth.LowOf(_prices[c], basis);
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
    /// those pivots make.
    /// </summary>
    private sealed record Leg(IReadOnlyList<SawtoothPivot> Inside, IReadOnlyList<Leg> Legs, IReadOnlyList<Swing> Swings)
    {
        public static readonly Leg None = new([], [], []);
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

    // A candle's high or low, as the basis has it.
    private double Extreme(int index, bool high) => high ? Sawtooth.HighOf(_prices[index], basis) : Sawtooth.LowOf(_prices[index], basis);
}
