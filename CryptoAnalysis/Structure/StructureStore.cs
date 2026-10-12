namespace Gradient.CryptoAnalysis;

/// <summary>
/// A pivot as the store has it at a candle: its id, its candle, kind and price, the level it lies at then, the candle
/// it became a pivot on, and the candle it could no longer move from, as the leg it was read in then has it.
/// </summary>
public sealed record StoredPivot(long Id, int Candle, EnumPivotKind Kind, double Price, int Level, int Known, int Confirmed);

/// <summary>
/// A confirmed swing as the store has it at a candle: its id, the candles of its start, of the pivot against it and of its
/// break of structure, the candle the leg it lies inside begins at, and the level it lies at then.
/// </summary>
public sealed record StoredSwing(long Id, EnumSwingDirection Direction, int Start, int Extreme, int Break, int Leg, int Level);

/// <summary>
/// A market structure break as the store has it at a candle: its id, the candle of the protective high or low and of the
/// close beyond it, the id of the swing that protected it, and the level it lies at then.
/// </summary>
public sealed record StoredBreak(long Id, EnumAnnotationType Type, int Reference, int Break, long Swing, int Level);

/// <summary>
/// A trend as the store has it at a candle: its id, the candles of its start, of the break of structure that confirmed it
/// and of the one that ended it (none while it goes on), its swings and the market structure breaks against it as counted
/// then, and the level it lies at then.
/// </summary>
public sealed record StoredTrend(long Id, EnumSwingDirection Direction, int Start, int Confirmed, int? End, int Swings, int MarketStructureBreaks,
    IReadOnlyList<TrendPart> Parts, int Level);

/// <summary>
/// One reading of a retracement: the candle of a new furthest point of the counter-move, and how far back that is as a
/// whole percentage of the move it retraces, rounded down.
/// </summary>
public sealed record StoredStep(int Candle, int Percent);

/// <summary>
/// A retracement as the store has it at a candle: its id, the candles the move it retraces runs from and to, its readings
/// up to then, whether its swing is confirmed, and the level it lies at then.
/// </summary>
public sealed record StoredRetracement(long Id, EnumSwingDirection Direction, int From, int To, IReadOnlyList<StoredStep> Steps, bool Confirmed, int Level);

/// <summary>
/// A candidate swing as the store has it at a candle: its id, the candle it starts at, the candle of the furthest
/// counter-move by then, and the level it lies at then.
/// </summary>
public sealed record StoredCandidate(long Id, EnumSwingDirection Direction, int Start, int Extreme, int Level);

/// <summary>
/// A range as the store has it at a candle: its id, the candles of where the move it retraces began, of its low and high
/// and of the break that identified it, the reading then, its band, the candle of the close that ended it (none while it
/// holds), and the level it lies at then.
/// </summary>
public sealed record StoredRange(long Id, EnumSwingDirection Direction, int From, int Low, int High, int Identified, int Retracement, double Band,
    int? End, int Level);

/// <summary>
/// The structure of a series of candles, worked out once and held: every pivot, confirmed swing, market structure break,
/// trend, retracement, candidate swing and range, each with an id, and how each stood from candle to candle. So what was
/// known at any candle, in any stretch, is looked up and not worked out again. A pivot's id is its candle and kind, and
/// every other id is the order the items of its kind were first found in, so each is the same every time the same candles
/// are read.
/// The ranges are those of the least retracement and band the store was read with.
/// It is held in memory. Reading every candle of a long series takes a few seconds, so a store can be written out and
/// read back in place of that (see <see cref="Save"/>), for as long as its candles are the same.
/// </summary>
public sealed class StructureStore
{
    // An item with how it stood from each candle that changed it, in candle order; no state while it is an item no longer.
    private sealed record Held<TItem, TState>(TItem Item, List<(int From, TState? State)> States) where TState : struct
    {
        public int Known => States[0].From;

        public TState? At(int at)
        {
            // The last change at or before the candle: often the last of all.
            if (States[^1].From <= at)
                return States[^1].State;
            var (low, high) = (0, States.Count - 1);
            while (low <= high)
            {
                var middle = (low + high) / 2;
                if (States[middle].From <= at)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            return high >= 0 ? States[high].State : null;
        }
    }

    private sealed record Pivot(long Id, int Candle, EnumPivotKind Kind, double Price, int Known);

    private sealed record Swing(long Id, ReadSwing Read);

    private readonly List<Price> _prices;
    private readonly EnumPriceBasis _basis;

    // The candles that changed a pivot.
    private readonly HashSet<int> _pivotsChanged;

    // The pivots in candle order, the swings in the order of their breaks of structure, the market structure breaks in
    // the order of their closes, and the rest in the order they were first found.
    private readonly List<Held<Pivot, (int Level, int Confirmed)>> _pivots;
    private readonly List<Held<Swing, int>> _swings;
    private readonly List<Held<StoredBreak, int>> _breaks;
    private readonly List<Held<ReadTrend, TrendState>> _trends;
    private readonly List<Held<ReadRetracement, int>> _retracements;
    private readonly List<Held<ReadCandidate, int>> _everyCandidate;
    private readonly List<Held<ReadRetracement, int>> _confirmed;
    private readonly List<Held<ReadRange, RangeState>> _ranges;

    // The candidates and their retracements, which come and go: those that hold in each stretch of candles.
    private const int Span = 256;
    private readonly List<Held<ReadRetracement, int>>[] _open;
    private readonly List<Held<ReadCandidate, int>>[] _candidates;

    private StructureStore(List<Price> prices, EnumPriceBasis basis, HashSet<int> pivotsChanged, List<Held<Pivot, (int Level, int Confirmed)>> pivots, List<Held<Swing, int>> swings,
        List<Held<StoredBreak, int>> breaks, List<Held<ReadTrend, TrendState>> trends, List<Held<ReadRetracement, int>> retracements,
        List<Held<ReadCandidate, int>> candidates, List<Held<ReadRange, RangeState>> ranges)
    {
        (_prices, _basis, _pivotsChanged) = (prices, basis, pivotsChanged);
        (_pivots, _swings, _breaks, _trends, _ranges, _retracements, _everyCandidate) = (pivots, swings, breaks, trends, ranges, retracements, candidates);
        _confirmed = retracements.Where(x => x.Item.Confirmed).ToList();
        _open = ByStretch(retracements.Where(x => !x.Item.Confirmed), prices.Count);
        _candidates = ByStretch(candidates, prices.Count);
    }

    /// <summary>
    /// How many candles the structure was read from.
    /// </summary>
    public int Candles => _prices.Count;

    /// <summary>
    /// The candles the structure was read from.
    /// </summary>
    public List<Price> Prices => _prices;

    /// <summary>
    /// Which prices its highs and lows are read from.
    /// </summary>
    public EnumPriceBasis Basis => _basis;

    /// <summary>
    /// Whether a candle changed any pivot: made one, moved its level, or unmade it.
    /// </summary>
    internal bool PivotsChanged(int candle) => _pivotsChanged.Contains(candle);

    /// <summary>
    /// The pivots at candles up to one, as known at its close, the last first: each with its level then.
    /// </summary>
    internal IEnumerable<StoredPivot> PivotsBack(int at)
    {
        for (var i = First(_pivots, x => x.Item.Candle > at) - 1; i >= 0; i--)
        {
            var pivot = _pivots[i].Item;
            if (_pivots[i].At(at) is { } state)
                yield return new StoredPivot(pivot.Id, pivot.Candle, pivot.Kind, pivot.Price, state.Level, pivot.Known, state.Confirmed);
        }
    }

    /// <summary>
    /// A candle's high or low, as the basis the store was read on has it.
    /// </summary>
    internal double Price(int candle, bool high) => _basis == EnumPriceBasis.Close ? _prices[candle].Close : high ? _prices[candle].High : _prices[candle].Low;

    /// <summary>
    /// Reads the candles one at a time and holds the structure of them.
    /// </summary>
    public static StructureStore Read(List<Price> prices, EnumPriceBasis basis = EnumPriceBasis.Close,
        double minRetracement = CryptoAnalysis.Ranges.DefaultMinRetracement, double band = CryptoAnalysis.Ranges.DefaultBand)
    {
        var pivots = new Dictionary<long, Held<Pivot, (int Level, int Confirmed)>>();
        var swings = new Dictionary<ReadSwing, Held<Swing, int>>();
        var breaks = new Dictionary<long, Held<StoredBreak, int>>();
        var trends = new Dictionary<long, Held<ReadTrend, TrendState>>();
        var retracements = new Dictionary<long, Held<ReadRetracement, int>>();
        var candidates = new Dictionary<long, Held<ReadCandidate, int>>();
        var ranges = new Dictionary<long, Held<ReadRange, RangeState>>();
        var reader = new StructureReader(basis, minRetracement: minRetracement, band: band);
        var changed = new HashSet<int>();
        for (var i = 0; i < prices.Count; i++)
        {
            reader.Add(prices[i]);
            var changes = reader.Changes;
            if (changes.Pivots.Count > 0)
                changed.Add(i);
            foreach (var change in changes.Pivots)
            {
                var id = PivotId(change.Candle, change.Kind);
                if (!pivots.TryGetValue(id, out var held))
                    pivots[id] = held = new Held<Pivot, (int Level, int Confirmed)>(new Pivot(id, change.Candle, change.Kind, change.Price, i), []);
                held.States.Add((i, change.Level is int level ? (level, change.Confirmed!.Value) : null));
            }

            foreach (var change in changes.Swings)
            {
                if (!swings.TryGetValue(change.Swing, out var held))
                    swings[change.Swing] = held = new Held<Swing, int>(new Swing(swings.Count + 1, change.Swing), []);
                held.States.Add((i, change.Level));
            }

            foreach (var change in changes.Breaks)
            {
                if (!breaks.TryGetValue(change.Break.Id, out var held))
                {
                    var read = change.Break;
                    breaks[read.Id] = held = new Held<StoredBreak, int>(new StoredBreak(read.Id, read.Type, read.Reference, read.Break, swings[read.Swing].Item.Id, 0), []);
                }

                held.States.Add((i, change.Level));
            }

            foreach (var change in changes.Trends)
                Hold(trends, change.Trend.Id, change.Trend, i, change.State);
            foreach (var change in changes.Retracements)
                Hold(retracements, change.Retracement.Id, change.Retracement, i, change.Level);
            foreach (var change in changes.Candidates)
                Hold(candidates, change.Candidate.Id, change.Candidate, i, change.Level);
            foreach (var change in changes.Ranges)
                Hold(ranges, change.Range.Id, change.Range, i, change.State);
        }

        return new StructureStore(prices, basis, changed,
            pivots.Values.OrderBy(x => x.Item.Id).ToList(),
            swings.Values.OrderBy(x => x.Item.Read.Break).ThenBy(x => x.Item.Id).ToList(),
            breaks.Values.OrderBy(x => x.Item.Break).ThenBy(x => x.Item.Id).ToList(),
            ByKnown(trends), ByKnown(retracements), ByKnown(candidates), ByKnown(ranges));
    }

    private static void Hold<TItem, TState>(Dictionary<long, Held<TItem, TState>> all, long id, TItem item, int candle, TState? state) where TState : struct
    {
        if (!all.TryGetValue(id, out var held))
            all[id] = held = new Held<TItem, TState>(item, []);
        held.States.Add((candle, state));
    }

    private static List<Held<TItem, TState>> ByKnown<TItem, TState>(Dictionary<long, Held<TItem, TState>> all) where TState : struct =>
        all.OrderBy(x => x.Value.Known).ThenBy(x => x.Key).Select(x => x.Value).ToList();

    // The number a kept store begins with: another when what is kept of one changes.
    private const int Kept = 0x53530001;

    /// <summary>
    /// Writes the store, all but its candles, to be read again by <see cref="Load"/> in place of reading every candle.
    /// </summary>
    public void Save(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        void States<TState>(List<(int From, TState? State)> states, Action<TState> write) where TState : struct
        {
            writer.Write(states.Count);
            foreach (var (from, state) in states)
            {
                writer.Write(from);
                writer.Write(state != null);
                if (state is { } has)
                    write(has);
            }
        }

        void Swing(ReadSwing swing)
        {
            writer.Write((byte)swing.Direction);
            writer.Write(swing.Start);
            writer.Write(swing.Extreme);
            writer.Write(swing.Break);
            writer.Write(swing.Leg);
        }

        void Steps(IReadOnlyList<int> steps)
        {
            writer.Write(steps.Count);
            foreach (var step in steps)
                writer.Write(step);
        }

        writer.Write(Kept);
        writer.Write(_prices.Count);
        writer.Write((byte)_basis);
        writer.Write(_pivotsChanged.Count);
        foreach (var candle in _pivotsChanged.Order())
            writer.Write(candle);

        writer.Write(_pivots.Count);
        foreach (var held in _pivots)
        {
            writer.Write(held.Item.Candle);
            writer.Write((byte)held.Item.Kind);
            writer.Write(held.Item.Price);
            writer.Write(held.Item.Known);
            States(held.States, x => { writer.Write(x.Level); writer.Write(x.Confirmed); });
        }

        writer.Write(_swings.Count);
        foreach (var held in _swings)
        {
            writer.Write(held.Item.Id);
            Swing(held.Item.Read);
            States(held.States, writer.Write);
        }

        writer.Write(_breaks.Count);
        foreach (var held in _breaks)
        {
            writer.Write(held.Item.Id);
            writer.Write((int)held.Item.Type);
            writer.Write(held.Item.Reference);
            writer.Write(held.Item.Break);
            writer.Write(held.Item.Swing);
            States(held.States, writer.Write);
        }

        writer.Write(_trends.Count);
        foreach (var held in _trends)
        {
            writer.Write(held.Item.Id);
            Swing(held.Item.First);
            States(held.States, x =>
            {
                writer.Write(x.Level);
                writer.Write(x.Confirmed);
                writer.Write(x.End ?? -1);
                writer.Write(x.MarketStructureBreaks);
                writer.Write(x.Parts.Count);
                foreach (var part in x.Parts)
                {
                    writer.Write(part.Break);
                    writer.Write(part.Strong);
                }
            });
        }

        writer.Write(_retracements.Count);
        foreach (var held in _retracements)
        {
            writer.Write(held.Item.Id);
            writer.Write((byte)held.Item.Direction);
            writer.Write(held.Item.From);
            writer.Write(held.Item.To);
            writer.Write(held.Item.End ?? -1);
            Steps(held.Item.Steps);
            States(held.States, writer.Write);
        }

        writer.Write(_everyCandidate.Count);
        foreach (var held in _everyCandidate)
        {
            writer.Write(held.Item.Id);
            writer.Write((byte)held.Item.Direction);
            writer.Write(held.Item.Start);
            Steps(held.Item.Steps);
            States(held.States, writer.Write);
        }

        writer.Write(_ranges.Count);
        foreach (var held in _ranges)
        {
            var range = held.Item;
            writer.Write(range.Id);
            writer.Write((byte)range.Direction);
            writer.Write(range.From);
            writer.Write(range.Low);
            writer.Write(range.High);
            writer.Write(range.Identified);
            writer.Write(range.Retracement);
            writer.Write(range.Band);
            States(held.States, x => { writer.Write(x.Level); writer.Write(x.End ?? -1); });
        }

        writer.Write(Kept);
    }

    /// <summary>
    /// The store of these candles as <see cref="Save"/> wrote it; null when what was written is not a store of this many
    /// candles on this basis, or is not whole. Whether the candles are the ones it was read from is for the caller to know,
    /// as by naming what is kept with their fingerprint.
    /// </summary>
    public static StructureStore? Load(Stream stream, List<Price> prices, EnumPriceBasis basis = EnumPriceBasis.Close)
    {
        try
        {
            using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            List<(int From, TState? State)> States<TState>(Func<TState> read) where TState : struct
            {
                var count = reader.ReadInt32();
                var states = new List<(int From, TState? State)>(count);
                for (var i = 0; i < count; i++)
                {
                    var from = reader.ReadInt32();
                    states.Add((from, reader.ReadBoolean() ? read() : null));
                }

                return states;
            }

            ReadSwing Swing() => new((EnumSwingDirection)reader.ReadByte(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());

            List<int> Steps()
            {
                var count = reader.ReadInt32();
                var steps = new List<int>(count);
                for (var i = 0; i < count; i++)
                    steps.Add(reader.ReadInt32());
                return steps;
            }

            List<T> Many<T>(Func<T> read)
            {
                var count = reader.ReadInt32();
                var all = new List<T>(count);
                for (var i = 0; i < count; i++)
                    all.Add(read());
                return all;
            }

            if (reader.ReadInt32() != Kept || reader.ReadInt32() != prices.Count || reader.ReadByte() != (byte)basis)
                return null;

            var changed = Many(reader.ReadInt32).ToHashSet();
            var pivots = Many(() =>
            {
                var (candle, kind) = (reader.ReadInt32(), (EnumPivotKind)reader.ReadByte());
                var pivot = new Pivot(PivotId(candle, kind), candle, kind, reader.ReadDouble(), reader.ReadInt32());
                return new Held<Pivot, (int Level, int Confirmed)>(pivot, States(() => (reader.ReadInt32(), reader.ReadInt32())));
            });
            var swings = Many(() => new Held<Swing, int>(new Swing(reader.ReadInt64(), Swing()), States(reader.ReadInt32)));
            var breaks = Many(() => new Held<StoredBreak, int>(
                new StoredBreak(reader.ReadInt64(), (EnumAnnotationType)reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt64(), 0), States(reader.ReadInt32)));
            var trends = Many(() =>
            {
                var (id, first) = (reader.ReadInt64(), Swing());
                return new Held<ReadTrend, TrendState>(new ReadTrend(id, first.Direction, first), States(() =>
                {
                    var (level, confirmed, end, against) = (reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                    var parts = new TrendPart[reader.ReadInt32()];
                    for (var i = 0; i < parts.Length; i++)
                        parts[i] = new TrendPart(reader.ReadInt32(), reader.ReadBoolean());
                    return new TrendState(level, confirmed, end < 0 ? null : end, against, parts);
                }));
            });
            var retracements = Many(() =>
            {
                var (id, direction, from, to, end) = (reader.ReadInt64(), (EnumSwingDirection)reader.ReadByte(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                return new Held<ReadRetracement, int>(new ReadRetracement(id, direction, from, to, end < 0 ? null : end, Steps()), States(reader.ReadInt32));
            });
            var candidates = Many(() =>
            {
                var (id, direction, start) = (reader.ReadInt64(), (EnumSwingDirection)reader.ReadByte(), reader.ReadInt32());
                return new Held<ReadCandidate, int>(new ReadCandidate(id, direction, start, Steps()), States(reader.ReadInt32));
            });
            var ranges = Many(() =>
            {
                var range = new ReadRange(reader.ReadInt64(), (EnumSwingDirection)reader.ReadByte(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
                    reader.ReadInt32(), reader.ReadDouble());
                return new Held<ReadRange, RangeState>(range, States(() =>
                {
                    var (level, end) = (reader.ReadInt32(), reader.ReadInt32());
                    return new RangeState(level, end < 0 ? null : end);
                }));
            });

            return reader.ReadInt32() == Kept ? new StructureStore(prices, basis, changed, pivots, swings, breaks, trends, retracements, candidates, ranges) : null;
        }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidCastException or OverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// The id of the pivot of a kind at a candle.
    /// </summary>
    public static long PivotId(int candle, EnumPivotKind kind) => candle * 2L + (kind == EnumPivotKind.High ? 1 : 0);

    /// <summary>
    /// The pivots at candles from to to, as known at the close of a candle (the last when none is given), each with its
    /// level then, at levels up to maxLevel; in candle order.
    /// </summary>
    public List<StoredPivot> Pivots(int from, int to, int? at = null, int maxLevel = int.MaxValue)
    {
        var known = At(at);
        var pivots = new List<StoredPivot>();
        for (var i = First(_pivots, x => x.Item.Candle >= from); i < _pivots.Count && _pivots[i].Item.Candle <= to; i++)
        {
            var pivot = _pivots[i].Item;
            if (_pivots[i].At(known) is { } state && state.Level <= maxLevel)
                pivots.Add(new StoredPivot(pivot.Id, pivot.Candle, pivot.Kind, pivot.Price, state.Level, pivot.Known, state.Confirmed));
        }

        return pivots;
    }

    /// <summary>
    /// The confirmed swings any part of which lies at candles from to to, as known at the close of a candle (the last when
    /// none is given), each with its level then, at levels up to maxLevel; in the order they broke structure.
    /// </summary>
    public List<StoredSwing> Swings(int from, int to, int? at = null, int maxLevel = int.MaxValue)
    {
        var known = At(at);
        var swings = new List<StoredSwing>();
        // A swing is not known before its break of structure.
        for (var i = First(_swings, x => x.Item.Read.Break >= from); i < _swings.Count && _swings[i].Item.Read.Break <= known; i++)
        {
            var swing = _swings[i].Item;
            if (swing.Read.Start <= to && _swings[i].At(known) is int level && level <= maxLevel)
                swings.Add(new StoredSwing(swing.Id, swing.Read.Direction, swing.Read.Start, swing.Read.Extreme, swing.Read.Break, swing.Read.Leg, level));
        }

        return swings;
    }

    /// <summary>
    /// The market structure breaks whose close lies at candles from to to, as known at the close of a candle (the last
    /// when none is given), each with its level then, at levels up to maxLevel; in the order of their closes.
    /// </summary>
    public List<StoredBreak> Breaks(int from, int to, int? at = null, int maxLevel = int.MaxValue)
    {
        var known = At(at);
        var breaks = new List<StoredBreak>();
        for (var i = First(_breaks, x => x.Item.Break >= from); i < _breaks.Count && _breaks[i].Item.Break <= Math.Min(to, known); i++)
        {
            if (_breaks[i].At(known) is int level && level <= maxLevel)
                breaks.Add(_breaks[i].Item with { Level = level });
        }

        return breaks;
    }

    /// <summary>
    /// The trends any part of which lies at candles from to to, as known at the close of a candle (the last when none is
    /// given), each as it stood then, at levels up to maxLevel; in the order they were first found. One that goes on runs
    /// to that candle.
    /// </summary>
    public List<StoredTrend> Trends(int from, int to, int? at = null, int maxLevel = int.MaxValue)
    {
        var known = At(at);
        var trends = new List<StoredTrend>();
        foreach (var held in _trends.TakeWhile(x => x.Known <= known))
        {
            var trend = held.Item;
            if (trend.First.Start <= to && held.At(known) is { } state && state.Level <= maxLevel && (state.End ?? known) >= from)
                trends.Add(new StoredTrend(trend.Id, trend.Direction, trend.First.Start, state.Confirmed, state.End, state.Swings, state.MarketStructureBreaks, state.Parts, state.Level));
        }

        return trends;
    }

    /// <summary>
    /// The retracements any part of which lies at candles from to to, from where the move retraced begins to the furthest
    /// point of the counter-move, as known at the close of a candle (the last when none is given), each with its readings
    /// up to then and its level then, at levels up to maxLevel; in the order they were first found. A candidate's runs to
    /// that candle.
    /// </summary>
    public List<StoredRetracement> Retracements(int from, int to, int? at = null, int maxLevel = int.MaxValue)
    {
        var known = At(at);
        var found = new List<Held<ReadRetracement, int>>();

        // A confirmed swing's readings were all made before it was known, so one known before the stretch lies before it.
        for (var i = First(_confirmed, x => x.Known >= from); i < _confirmed.Count && _confirmed[i].Known <= known; i++)
        {
            if (_confirmed[i].Item.From <= to && _confirmed[i].Item.Steps[^1] >= from)
                found.Add(_confirmed[i]);
        }

        found.AddRange(_open[known / Span].Where(x => x.Item.From <= to));

        var retracements = new List<StoredRetracement>();
        foreach (var held in found.OrderBy(x => x.Known).ThenBy(x => x.Item.Id))
        {
            var read = held.Item;
            if (held.At(known) is not int level || level > maxLevel)
                continue;

            var steps = read.Confirmed ? read.Steps : read.Steps.TakeWhile(x => x <= known).ToList();
            if (steps.Count == 0)
                continue;

            // Each reading is how far back along the move its candle's price is, as Sawtooth.Retracement reads it.
            var up = read.Direction == EnumSwingDirection.Up;
            var end = Price(read.To, up);
            var gap = up ? end - Price(read.From, false) : Price(read.From, true) - end;
            retracements.Add(new StoredRetracement(read.Id, read.Direction, read.From, read.To,
                steps.Select(x => new StoredStep(x, (int)Math.Floor(100 * (up ? end - Price(x, false) : Price(x, true) - end) / gap))).ToList(), read.Confirmed, level));
        }

        return retracements;
    }

    /// <summary>
    /// The candidate swings as known at the close of a candle (the last when none is given), each with the furthest
    /// counter-move by then and its level then, at levels up to maxLevel; in the order they were first found.
    /// </summary>
    public List<StoredCandidate> Candidates(int? at = null, int maxLevel = int.MaxValue)
    {
        var known = At(at);
        var candidates = new List<StoredCandidate>();
        foreach (var held in _candidates[known / Span])
        {
            var candidate = held.Item;
            if (held.At(known) is int level && level <= maxLevel)
                candidates.Add(new StoredCandidate(candidate.Id, candidate.Direction, candidate.Start, candidate.Steps.TakeWhile(x => x <= known).Last(), level));
        }

        return candidates;
    }

    // The items that hold at any candle of each stretch of Span candles, a stretch after another, each in the order the
    // items are given: so those that hold at a candle are looked for among the few of its stretch, not among them all.
    private static List<Held<TItem, TState>>[] ByStretch<TItem, TState>(IEnumerable<Held<TItem, TState>> all, int candles) where TState : struct
    {
        var stretches = Enumerable.Range(0, candles / Span + 1).Select(_ => new List<Held<TItem, TState>>()).ToArray();
        foreach (var held in all)
        {
            var filled = -1;
            for (var k = 0; k < held.States.Count; k++)
            {
                if (held.States[k].State == null)
                    continue;
                var until = k + 1 < held.States.Count ? held.States[k + 1].From - 1 : candles - 1;
                for (var s = Math.Max(held.States[k].From / Span, filled + 1); s <= until / Span; s++)
                    stretches[s].Add(held);
                filled = Math.Max(filled, until / Span);
            }
        }

        return stretches;
    }

    /// <summary>
    /// The ranges any part of which lies at candles from to to, from where the move retraced begins, as known at the
    /// close of a candle (the last when none is given), each as it stood then, at levels up to maxLevel; in the order they
    /// were first found. One that holds runs to that candle.
    /// </summary>
    public List<StoredRange> Ranges(int from, int to, int? at = null, int maxLevel = int.MaxValue)
    {
        var known = At(at);
        var ranges = new List<StoredRange>();
        foreach (var held in _ranges.TakeWhile(x => x.Known <= known))
        {
            var range = held.Item;
            if (range.From <= to && held.At(known) is { } state && state.Level <= maxLevel && (state.End ?? known) >= from)
                ranges.Add(new StoredRange(range.Id, range.Direction, range.From, range.Low, range.High, range.Identified, range.Retracement, range.Band, state.End, state.Level));
        }

        return ranges;
    }

    private int At(int? at) => Math.Clamp(at ?? Candles - 1, 0, Math.Max(0, Candles - 1));

    // The first of a list in order that a test holds for, and for all after it; the list's count when it holds for none.
    private static int First<T>(List<T> items, Func<T, bool> test)
    {
        var (low, high) = (0, items.Count);
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (test(items[middle]))
                high = middle;
            else
                low = middle + 1;
        }

        return low;
    }
}
