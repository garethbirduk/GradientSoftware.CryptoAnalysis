namespace Gradient.CryptoAnalysis;

/// <summary>
/// A Swing that the search inside each leg of the level above leaves out: two points of a level one after the other, a
/// high and the low after it or a low and the high after it, against the direction of the leg of the level above that
/// the first lies in, and a close beyond the first before the level's next point. Across is true when the second point
/// is a point of the level above, so that the Swing runs from one of its legs into the next; otherwise it lies inside
/// the one leg. Start, Extreme and Break are candles.
/// </summary>
public sealed record OverlappingSwing(int Level, EnumSwingDirection Direction, int Start, int Extreme, int Break, bool Across);

/// <summary>
/// The Swings that go against the leg of the level above (see <see cref="OverlappingSwing"/>), read from a level's
/// points and those of the level above as known at a candle. A pair both of whose points are points of the level above
/// too is that level's, and is left to it.
/// </summary>
public static class OverlappingSwings
{
    /// <summary>
    /// Those of a level whose break lies at candles from to to, as known at a candle: the level's points and those of
    /// the level above as known then, in candle order, and the price that a close is read at.
    /// </summary>
    public static List<OverlappingSwing> Of(IReadOnlyList<SawtoothPivot> points, IReadOnlyList<SawtoothPivot> above, int level,
        Func<int, bool, double> price, int at, int from, int to)
    {
        var found = new List<OverlappingSwing>();
        var turns = points.Where(p => p.Kind is EnumPivotKind.High or EnumPivotKind.Low).ToList();
        var aboveTurns = above.Where(p => p.Kind is EnumPivotKind.High or EnumPivotKind.Low).Select(p => p.Index).ToHashSet();
        if (turns.Count < 2 || above.Count < 2)
            return found;

        // A break lies before the point after the pair, so a pair two points before the first at or after from is the
        // earliest that can break there.
        var first = turns.FindIndex(p => p.Index >= from);
        for (var t = Math.Max(0, (first < 0 ? turns.Count : first) - 2); t + 1 < turns.Count && turns[t].Index <= to; t++)
        {
            var (start, counter) = (turns[t], turns[t + 1]);
            if (aboveTurns.Contains(start.Index) && aboveTurns.Contains(counter.Index))
                continue;

            var up = start.Kind == EnumPivotKind.High;
            if (up == LegIsUp(above, start.Index))
                continue;

            var limit = Math.Min(t + 2 < turns.Count ? turns[t + 2].Index : at, at);
            for (var c = counter.Index + 1; c <= limit; c++)
            {
                var close = price(c, up);
                if (up ? close > start.Price : close < start.Price)
                {
                    if (c >= from && c <= to)
                        found.Add(new OverlappingSwing(level, up ? EnumSwingDirection.Up : EnumSwingDirection.Down, start.Index, counter.Index, c, aboveTurns.Contains(counter.Index)));
                    break;
                }
            }
        }

        return found;
    }

    // Whether the leg of the level above that a candle lies in runs up: to a high, or, the last leg, from a low.
    private static bool LegIsUp(IReadOnlyList<SawtoothPivot> above, int candle)
    {
        var (low, high) = (0, above.Count - 1);
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (above[middle].Index <= candle)
                low = middle;
            else
                high = middle - 1;
        }

        var (legFrom, legTo) = low + 1 < above.Count ? (above[low], above[low + 1]) : (above[Math.Max(0, low - 1)], above[low]);
        return legTo.Kind == EnumPivotKind.High || (legTo.Kind == EnumPivotKind.Current && legFrom.Kind == EnumPivotKind.Low);
    }
}
