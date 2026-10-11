namespace Gradient.CryptoAnalysis.Site;

/// <summary>
/// A pivot as the store has it at a candle: its id, its candle, kind and price, the level it lies at then, and the candle
/// it became a pivot on.
/// </summary>
public sealed record StoredPivot(long Id, int Candle, EnumPivotKind Kind, double Price, int Level, int Known);

/// <summary>
/// A confirmed swing as the store has it at a candle: its id, the candles of its start, of the pivot against it and of its
/// break of structure, the candle the leg it lies inside begins at, and the level it lies at then.
/// </summary>
public sealed record StoredSwing(long Id, EnumSwingDirection Direction, int Start, int Extreme, int Break, int Leg, int Level);

/// <summary>
/// The structure of a series of candles, worked out once and held: every pivot and confirmed swing, each with an id, and
/// the level each lay at from candle to candle. So what was known at any candle, in any stretch, is looked up and not
/// worked out again. A pivot's id is its candle and kind and a swing's the order it was confirmed in, so both are the same
/// each time the same candles are read.
/// It is held in memory and read again when the candles change: reading every candle of a long series takes about a second.
/// </summary>
public sealed class StructureStore
{
    // An item with the level it lay at from each candle its level changed on, in candle order; no level once it is an item
    // no longer.
    private sealed record Held<T>(T Item, List<(int From, int? Level)> Levels)
    {
        public int? LevelAt(int at)
        {
            // The last change at or before the candle.
            var (low, high) = (0, Levels.Count - 1);
            while (low <= high)
            {
                var middle = (low + high) / 2;
                if (Levels[middle].From <= at)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            return high >= 0 ? Levels[high].Level : null;
        }
    }

    private sealed record Pivot(long Id, int Candle, EnumPivotKind Kind, double Price, int Known);

    private sealed record Swing(long Id, ReadSwing Read);

    // The pivots in candle order, and the swings in the order of their breaks of structure.
    private readonly List<Held<Pivot>> _pivots;
    private readonly List<Held<Swing>> _swings;

    private StructureStore(int candles, List<Held<Pivot>> pivots, List<Held<Swing>> swings)
    {
        Candles = candles;
        _pivots = pivots;
        _swings = swings;
    }

    /// <summary>
    /// How many candles the structure was read from.
    /// </summary>
    public int Candles { get; }

    /// <summary>
    /// Reads the candles one at a time and holds the structure of them.
    /// </summary>
    public static StructureStore Read(List<Price> prices, EnumPriceBasis basis = EnumPriceBasis.Close)
    {
        var pivots = new Dictionary<long, Held<Pivot>>();
        var swings = new Dictionary<ReadSwing, Held<Swing>>();
        var reader = new SawtoothReader(basis);
        for (var i = 0; i < prices.Count; i++)
        {
            reader.Add(prices[i]);
            foreach (var change in reader.Changes.Pivots)
            {
                var id = PivotId(change.Candle, change.Kind);
                if (!pivots.TryGetValue(id, out var held))
                    pivots[id] = held = new Held<Pivot>(new Pivot(id, change.Candle, change.Kind, change.Price, i), []);
                held.Levels.Add((i, change.Level));
            }

            foreach (var change in reader.Changes.Swings)
            {
                if (!swings.TryGetValue(change.Swing, out var held))
                    swings[change.Swing] = held = new Held<Swing>(new Swing(swings.Count + 1, change.Swing), []);
                held.Levels.Add((i, change.Level));
            }
        }

        return new StructureStore(prices.Count, pivots.Values.OrderBy(x => x.Item.Id).ToList(), swings.Values.OrderBy(x => x.Item.Read.Break).ThenBy(x => x.Item.Id).ToList());
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
            if (_pivots[i].LevelAt(known) is int level && level <= maxLevel)
                pivots.Add(new StoredPivot(pivot.Id, pivot.Candle, pivot.Kind, pivot.Price, level, pivot.Known));
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
            if (swing.Read.Start <= to && _swings[i].LevelAt(known) is int level && level <= maxLevel)
                swings.Add(new StoredSwing(swing.Id, swing.Read.Direction, swing.Read.Start, swing.Read.Extreme, swing.Read.Break, swing.Read.Leg, level));
        }

        return swings;
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
