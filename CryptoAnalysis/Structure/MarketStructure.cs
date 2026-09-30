namespace Gradient.CryptoAnalysis;

/// <summary>
/// The swings, market structure breaks and trends at one sawtooth level.
/// </summary>
public sealed record LevelStructure(
    int Level,
    IReadOnlyList<SwingOutline> Swings,
    IReadOnlyList<MarketStructureBreakOutline> MarketStructureBreaks,
    IReadOnlyList<TrendOutline> Trends);

/// <summary>
/// Something that became known on a candle at a sawtooth level: a break of structure, a market structure break, or a trend
/// being confirmed. Price is the value that did it.
/// </summary>
public sealed record StructureEvent(EnumAnnotationType Type, EnumSwingDirection Direction, int Level, DateTime Time, double Price);

/// <summary>
/// A sawtooth pivot with its level.
/// </summary>
public sealed record LevelPivot(int Level, SawtoothPivot Pivot);

/// <summary>
/// A market structure break with its level.
/// </summary>
public sealed record LevelBreak(int Level, MarketStructureBreakOutline Break);

/// <summary>
/// How a replay entry stopped holding: its item changed (Updated, e.g. a candidate's pullback deepened), a candidate became a
/// swing (Confirmed), or the item went away because a later new high redrew the levels (Removed).
/// </summary>
public enum EnumReplayEnd
{
    Updated,
    Confirmed,
    Removed,
}

/// <summary>
/// One state of an item in a replay: Value holds, as seen live, from candle From until the candle before Until (to the end
/// when Until is null). Continued is true when it follows an Updated entry for the same item.
/// </summary>
public sealed record ReplayEntry<T>(T Value, int From, int? Until, EnumReplayEnd? Ended, bool Continued);

/// <summary>
/// Everything a replay draws, as it was known on each candle: the sawtooth pivots, structure points, swings, candidate swings,
/// market structure breaks and trends at every level, and the candle runs, each with the candles it held for. Seen at a
/// candle, the entries that hold there are the structure computed from the prices up to that candle. The current price pivot is left out: at every level
/// it is that candle's close, unless the level already ends there.
/// </summary>
public sealed record ReplayTimeline(
    List<ReplayEntry<LevelPivot>> Pivots,
    List<ReplayEntry<SwingOutline>> Swings,
    List<ReplayEntry<CandidateSwing>> Candidates,
    List<ReplayEntry<LevelBreak>> MarketStructureBreaks,
    List<ReplayEntry<TrendOutline>> Trends,
    List<ReplayEntry<StructurePoint>> Points,
    List<ReplayEntry<CandleRun>> CandleRuns);

/// <summary>
/// Market structure as it can be known at a candle, from that candle's prices and earlier ones only.
/// The sawtooth over a whole series uses hindsight: its levels are anchored at the series high, which may come later.
/// Computing from the prices up to a candle is what could have been seen live, so it is what backtests must use. The two can
/// differ: a pullback that later proves to be inside an upleg is, until then, the presumed death-leg, with its own downswings.
/// </summary>
public static class MarketStructure
{
    /// <summary>
    /// Returns the swings, market structure breaks and trends at a level, from these prices alone.
    /// </summary>
    public static LevelStructure At(List<Price> prices, EnumPriceBasis basis, int level, int minTrendSwings = 2)
    {
        var levels = Sawtooth.Levels(prices, basis, level);
        var swings = Sawtooth.Swings(prices, levels, basis, level).OrderBy(x => x.Start.Time).ToList();
        var breaks = Sawtooth.MarketStructureBreaks(prices, swings, basis);
        return new LevelStructure(level, swings, breaks, Sawtooth.Trends(swings, breaks, minTrendSwings));
    }

    /// <summary>
    /// Returns what becomes known on the last candle at a level, from these prices alone: breaks of structure, market structure
    /// breaks and trends confirmed on it.
    /// </summary>
    public static List<StructureEvent> EventsOnLastCandle(List<Price> prices, EnumPriceBasis basis, int level, int minTrendSwings = 2)
    {
        if (prices.Count == 0)
            return [];

        var last = prices[^1].DateTime;
        var structure = At(prices, basis, level, minTrendSwings);
        var events = new List<StructureEvent>();

        events.AddRange(structure.Swings
            .Where(x => x.BreakOfStructure?.Time == last)
            .Select(x => new StructureEvent(
                x.Direction == EnumSwingDirection.Up ? EnumAnnotationType.BullishBreakOfStructure : EnumAnnotationType.BearishBreakOfStructure,
                x.Direction, level, last, x.BreakOfStructure!.Price)));

        events.AddRange(structure.MarketStructureBreaks
            .Where(x => x.Break.Time == last)
            .Select(x => new StructureEvent(x.Type,
                x.Type == EnumAnnotationType.BullishMarketStructureBreak ? EnumSwingDirection.Up : EnumSwingDirection.Down,
                level, last, x.Break.Price)));

        events.AddRange(structure.Trends
            .Where(x => x.Confirmed.Time == last)
            .Select(x => new StructureEvent(
                x.Direction == EnumSwingDirection.Up ? EnumAnnotationType.Uptrend : EnumAnnotationType.Downtrend,
                x.Direction, level, last, x.Confirmed.Price)));

        return events.OrderBy(x => x.Type).ToList();
    }

    /// <summary>
    /// Replays the prices one candle at a time, computing the structure at every level from the prices up to each candle, and
    /// records how long each pivot, swing, candidate, market structure break and trend held. The levels are anchored at the
    /// first price, however far into the series the replay starts.
    /// Only candles from..to (exclusive) are replayed: items that already hold at from start there. When keepFrom is given,
    /// items that end before that candle are left out (a pivot line keeps the one pivot before it), so a replay deep into a
    /// long series only carries what a chart of its recent candles can show.
    /// </summary>
    public static ReplayTimeline Timeline(List<Price> prices, EnumPriceBasis basis, int maxLevel = 8, int minTrendSwings = 2,
        int from = 0, int? to = null, int? keepFrom = null)
    {
        var end = Math.Min(to ?? prices.Count, prices.Count);
        from = Math.Max(0, from);
        var snapshots = new Snapshot[Math.Max(0, end - from)];
        Parallel.For(from, end, t => snapshots[t - from] = TakeSnapshot(prices, basis, t, maxLevel, minTrendSwings, keepFrom));

        var pivots = new Tracker<LevelPivot>(x => (x.Level, x.Pivot.Kind, x.Pivot.Index));
        var swings = new Tracker<SwingOutline>(x => (x.Level, x.Direction, x.Start.Time));
        var candidates = new Tracker<CandidateSwing>(x => (x.Level, x.Direction, x.Start.Time), x => (x.Direction, x.Start.Time));
        var breaks = new Tracker<LevelBreak>(x => (x.Level, x.Break.Type, x.Break.Break.Time));
        var trends = new Tracker<TrendOutline>(x => (x.Level, x.Direction, x.Start.Time));
        // A point that moves (a higher close moving an HH forward) stays the same item: its leg starts from the same place.
        var points = new Tracker<StructurePoint>(x => (x.Level, x.Kind, x.LegStart));
        var runs = new Tracker<CandleRun>(x => (x.Green, x.Start.Time));

        for (var t = from; t < end; t++)
        {
            var s = snapshots[t - from];
            // A candidate is confirmed when a swing with its start appears at any level: the break may also promote it.
            var confirmed = s.Swings.Select(x => (object)(x.Direction, x.Start.Time)).ToHashSet();
            pivots.Step(t, s.Pivots);
            points.Step(t, s.Points);
            runs.Step(t, s.Runs);
            swings.Step(t, s.Swings);
            candidates.Step(t, s.Candidates, confirmed);
            breaks.Step(t, s.Breaks);
            trends.Step(t, s.Trends);
        }

        return new ReplayTimeline(pivots.Entries(), swings.Entries(), candidates.Entries(), breaks.Entries(), trends.Entries(),
            points.Entries(), runs.Entries());
    }

    private sealed record Snapshot(
        List<LevelPivot> Pivots,
        List<StructurePoint> Points,
        List<CandleRun> Runs,
        List<SwingOutline> Swings,
        List<CandidateSwing> Candidates,
        List<LevelBreak> Breaks,
        List<TrendOutline> Trends);

    /// <summary>
    /// Computes everything the replay tracks at candle t, from the prices up to it.
    /// </summary>
    private static Snapshot TakeSnapshot(List<Price> prices, EnumPriceBasis basis, int t, int maxLevel, int minTrendSwings, int? keepFrom)
    {
        var prefix = prices.GetRange(0, t + 1);
        var levels = Sawtooth.Levels(prefix, basis, maxLevel);
        var keep = keepFrom is int k && k > 0 ? prices[Math.Min(k, t)].DateTime : DateTime.MinValue;

        var pivots = new List<LevelPivot>();
        foreach (var level in levels)
        {
            var kept = level.Pivots.Where(p => p.Kind != EnumPivotKind.Current).ToList();
            var first = Math.Max(0, kept.FindLastIndex(p => p.Time < keep));
            pivots.AddRange(kept.Skip(keep == DateTime.MinValue ? 0 : first).Select(p => new LevelPivot(level.Level, p)));
        }

        var swings = new List<SwingOutline>();
        var candidates = new List<CandidateSwing>();
        var breaks = new List<LevelBreak>();
        var trends = new List<TrendOutline>();
        for (var level = 1; level <= Math.Min(maxLevel, levels.Count); level++)
        {
            var levelSwings = Sawtooth.Swings(prefix, levels, basis, level).OrderBy(x => x.Start.Time).ToList();
            var levelBreaks = Sawtooth.MarketStructureBreaks(prefix, levelSwings, basis);
            swings.AddRange(levelSwings.Where(x => x.End.Time >= keep));
            breaks.AddRange(levelBreaks.Where(x => x.Break.Time >= keep).Select(x => new LevelBreak(level, x)));
            trends.AddRange(Sawtooth.Trends(levelSwings, levelBreaks, minTrendSwings).Where(x => x.End == null || x.End.Time >= keep));
            candidates.AddRange(Sawtooth.Candidates(prefix, levels, basis, level));
        }

        return new Snapshot(
            pivots,
            levels.SelectMany(l => Sawtooth.Points(prefix, l, basis)).Where(x => x.Time >= keep).ToList(),
            CandleRuns.Runs(prefix, minLength: 2).Where(x => x.End.Time >= keep).ToList(),
            swings, candidates, breaks, trends);
    }

    /// <summary>
    /// Follows a set of items over candles, closing an entry whenever an item changes or goes away.
    /// </summary>
    private sealed class Tracker<T>(Func<T, object> key, Func<T, object>? confirmKey = null) where T : notnull
    {
        private readonly List<ReplayEntry<T>> _closed = [];
        private readonly Dictionary<T, (int From, bool Continued)> _open = [];

        public void Step(int t, IEnumerable<T> current, IReadOnlySet<object>? confirmed = null)
        {
            var now = current.ToHashSet();
            var keys = now.Select(key).ToHashSet();
            var updated = new HashSet<object>();

            foreach (var (value, open) in _open.Where(x => !now.Contains(x.Key)).ToList())
            {
                var k = key(value);
                var ended = keys.Contains(k) ? EnumReplayEnd.Updated
                    : confirmKey != null && confirmed?.Contains(confirmKey(value)) == true ? EnumReplayEnd.Confirmed
                    : EnumReplayEnd.Removed;
                if (ended == EnumReplayEnd.Updated)
                    updated.Add(k);
                _closed.Add(new ReplayEntry<T>(value, open.From, t, ended, open.Continued));
                _open.Remove(value);
            }

            foreach (var value in now.Where(x => !_open.ContainsKey(x)))
                _open[value] = (t, updated.Contains(key(value)));
        }

        public List<ReplayEntry<T>> Entries()
        {
            return _closed
                .Concat(_open.Select(x => new ReplayEntry<T>(x.Key, x.Value.From, null, null, x.Value.Continued)))
                .OrderBy(x => x.From)
                .ToList();
        }
    }

    /// <summary>
    /// Replays the prices one candle at a time and returns each event on the candle it became known, computed from the prices
    /// up to that candle (the last window candles of them, when a window is given). This is the structure as it was seen live.
    /// </summary>
    public static List<StructureEvent> Replay(List<Price> prices, EnumPriceBasis basis, int level, int? window = null, int minTrendSwings = 2)
    {
        var events = new List<StructureEvent>();
        for (var t = 0; t < prices.Count; t++)
        {
            var from = window is int size ? Math.Max(0, t + 1 - size) : 0;
            events.AddRange(EventsOnLastCandle(prices.GetRange(from, t + 1 - from), basis, level, minTrendSwings));
        }

        return events;
    }
}
