using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Gradient.CryptoAnalysis;

/// <summary>
/// A range found by swings, by its candles: the price stayed inside the last leg of a level, between its two ends, while
/// the level after it confirmed a count of swings. Leg is the candle of the leg's own end, the high or low those swings
/// began from, and Other the candle of the end it came from, a point of a coarser level on the other side. Identified is
/// the candle of the break of structure that made the count, and End the candle of the first close beyond either end
/// (none while it holds). Level is the level the leg lay at when it was identified, Inner the level of the swings
/// counted, one after it, and LegLevel the level the point at the leg's own end had then.
/// </summary>
public sealed record SwingRange(int Level, int Inner, EnumPivotKind LegKind, int LegLevel, int Leg, int Other, int Identified, int? End, int Swings, int Weak);

/// <summary>
/// The ranges found by swings in a store's candles (see <see cref="SwingRange"/>), each from what was known at the candle
/// that identified it. Its two ends are fixed from then on, so what holds it is the price alone, and it is never unmade.
/// </summary>
public static class SwingRanges
{
    /// <summary>
    /// How many swings one level finer are counted, when no count is given.
    /// </summary>
    public const int DefaultSwings = 4;

    // Each store's ranges once for each count, kept while the store is.
    private static readonly ConditionalWeakTable<StructureStore, ConcurrentDictionary<int, Lazy<List<SwingRange>>>> Found = [];

    /// <summary>
    /// Every range the store's candles have for a count of swings, in the order they were identified; found once a store
    /// and count and kept.
    /// </summary>
    public static IReadOnlyList<SwingRange> Find(StructureStore store, int swings = DefaultSwings) =>
        Found.GetValue(store, _ => new()).GetOrAdd(Math.Max(1, swings), count => new Lazy<List<SwingRange>>(() => Read(store, count))).Value;

    /// <summary>
    /// The ranges any part of which lies at candles from to to, as known at the close of a candle: those identified by
    /// then, each with its end when that had come, the level its leg lies at then, and the swings counted inside it by
    /// then, with how many of them are weak.
    /// </summary>
    public static List<SwingRange> Over(StructureStore store, int swings, int from, int to, int at)
    {
        var found = new List<SwingRange>();
        foreach (var range in Find(store, swings))
        {
            if (range.Identified > at)
                break;
            var end = range.End is int e && e <= at ? range.End : null;
            if (range.Identified > to || (end ?? at) < from)
                continue;

            // The leg's level as it is now, while its end is still a point of the structure; the swings inside it are that
            // many levels after it as they were.
            var seen = end ?? at;
            if (store.Pivots(range.Leg, range.Leg, seen).FirstOrDefault(x => x.Kind == range.LegKind) is not { } leg)
            {
                found.Add(range with { End = end });
                continue;
            }

            var level = range.Level + (leg.Level - range.LegLevel);
            var inner = level + (range.Inner - range.Level);
            var (count, weak) = Inside(store, range.Leg, inner, seen);
            found.Add(range with { Level = level, Inner = inner, End = end, Swings = Math.Max(count, range.Swings), Weak = Math.Max(weak, range.Weak) });
        }

        return found;
    }

    /// <summary>
    /// The swings counted inside a range, as <see cref="Over"/> gives it known at a candle: those of its inner level
    /// confirmed inside its leg by then, or by its end when that had come, in the order they broke structure.
    /// </summary>
    public static List<StoredSwing> Inner(StructureStore store, SwingRange range, int at)
    {
        var seen = Math.Min(range.End ?? at, at);
        return store.Swings(range.Leg, seen, seen).Where(x => x.Level == range.Inner && x.Leg == range.Leg).ToList();
    }

    // The swings of a level confirmed inside the leg that begins at a candle, as known at a candle, and how many of them
    // are weak: have a market structure break against them between their start and their break of structure.
    private static (int Swings, int Weak) Inside(StructureStore store, int leg, int level, int at)
    {
        var (count, weak) = (0, 0);
        foreach (var swing in store.Swings(leg, at, at))
        {
            if (swing.Level != level || swing.Leg != leg)
                continue;
            count++;
            var against = swing.Direction == EnumSwingDirection.Up ? EnumAnnotationType.BearishMarketStructureBreak : EnumAnnotationType.BullishMarketStructureBreak;
            if (store.Breaks(swing.Start + 1, swing.Break - 1, at).Any(x => x.Level == level && x.Type == against))
                weak++;
        }

        return (count, weak);
    }

    private static List<SwingRange> Read(StructureStore store, int swings)
    {
        var prices = store.Prices;
        var found = new List<SwingRange>();
        var seen = new HashSet<(int Leg, int Level)>();
        for (var t = 0; t < store.Candles; t++)
        {
            foreach (var swing in store.Swings(t, t, t))
            {
                if (swing.Level < 2 || seen.Contains((swing.Leg, swing.Level)))
                    continue;
                var (count, weak) = Inside(store, swing.Leg, swing.Level, t);
                if (count < swings)
                    continue;
                seen.Add((swing.Leg, swing.Level));
                if (store.Pivots(swing.Leg, swing.Leg, t).FirstOrDefault() is not { } own || own.Level < 1)
                    continue;

                // The end the leg came from: the last point of a coarser level before it, on the other side.
                StoredPivot? other = null;
                foreach (var back in new[] { 300, 3000, 30000, int.MaxValue })
                {
                    other = store.Pivots(Math.Max(0, swing.Leg - Math.Min(back, swing.Leg)), swing.Leg - 1, t, own.Level - 1).LastOrDefault(x => x.Kind != own.Kind);
                    if (other != null || back > swing.Leg)
                        break;
                }

                if (other == null)
                    continue;
                var (high, low) = own.Kind == EnumPivotKind.High ? (own.Price, other.Price) : (other.Price, own.Price);
                if (!(high > low) || prices[t].Close >= high || prices[t].Close <= low)
                    continue;

                // It holds until a close beyond either end.
                var end = t + 1;
                while (end < store.Candles && prices[end].Close < high && prices[end].Close > low)
                    end++;
                found.Add(new SwingRange(swing.Level - 1, swing.Level, own.Kind, own.Level, swing.Leg, other.Candle, t, end < store.Candles ? end : null, count, weak));
            }
        }

        return found;
    }
}
