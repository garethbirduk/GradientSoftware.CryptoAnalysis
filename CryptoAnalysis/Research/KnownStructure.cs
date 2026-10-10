using System.Runtime.CompilerServices;

namespace Gradient.CryptoAnalysis.Research;

/// <summary>
/// What market structure showed at each candle's close, as a chart of the last Window candles to that one would show it:
/// worked out from those candles alone, so it is what could have been known then. A level is that chart's level, so level 1
/// is the structure of the window as a whole and each level after it finer. Working from a window rather than every candle
/// before keeps the cost of a candle the same however long the series, where the replay's grows with it.
/// </summary>
public static class KnownStructure
{
    public const int Window = 500;
    public const int Levels = 4;

    /// <summary>
    /// What a level can show at a candle: a break of structure up or down on it, a market structure break up or down on it
    /// (bullish or bearish), or an uptrend or downtrend holding.
    /// </summary>
    public enum EnumFlag
    {
        BreakUp,
        BreakDown,
        BullishMarketStructureBreak,
        BearishMarketStructureBreak,
        Uptrend,
        Downtrend,
    }

    private static readonly int Flags = Enum.GetValues<EnumFlag>().Length;

    private static readonly IReadOnlyList<KnownBreak> None = [];

    // Each series' flags and breaks once, kept while the series is.
    private static readonly ConditionalWeakTable<IReadOnlyList<Price>, Lazy<(int[] Masks, IReadOnlyList<KnownBreak>[] Breaks)>> Known = [];

    private static (int[] Masks, IReadOnlyList<KnownBreak>[] Breaks) Of(IReadOnlyList<Price> prices) =>
        Known.GetValue(prices, p => new Lazy<(int[], IReadOnlyList<KnownBreak>[])>(() => Read(p, EnumPriceBasis.Close, Window, Levels))).Value;

    /// <summary>
    /// The candles at which a level shows a flag, worked out once a series and kept.
    /// </summary>
    public static bool[] Met(IReadOnlyList<Price> prices, int level, EnumFlag flag)
    {
        var bit = 1 << Bit(level, flag);
        return Of(prices).Masks.Select(x => (x & bit) != 0).ToArray();
    }

    /// <summary>
    /// The breaks each candle's close made, at every level, each with what it was read from (see <see cref="KnownBreak"/>);
    /// worked out once a series and kept.
    /// </summary>
    public static IReadOnlyList<KnownBreak>[] Breaks(IReadOnlyList<Price> prices) => Of(prices).Breaks;

    /// <summary>
    /// Each candle's flags at levels 1 to levels, a bit a level and flag (see Bit), each from the window of candles to it.
    /// </summary>
    public static int[] Masks(IReadOnlyList<Price> prices, EnumPriceBasis basis, int window, int levels) => Read(prices, basis, window, levels).Masks;

    /// <summary>
    /// Each candle's flags, as <see cref="Masks"/> gives them, and the breaks its close made, each from the window of candles
    /// to it.
    /// </summary>
    public static (int[] Masks, IReadOnlyList<KnownBreak>[] Breaks) Read(IReadOnlyList<Price> prices, EnumPriceBasis basis, int window, int levels)
    {
        var list = prices as List<Price> ?? prices.ToList();
        var masks = new int[list.Count];
        var breaks = new IReadOnlyList<KnownBreak>[list.Count];
        Parallel.For(0, list.Count, t =>
        {
            var from = Math.Max(0, t + 1 - window);
            (masks[t], breaks[t]) = Seen(list.GetRange(from, t + 1 - from), from, basis, levels);
        });
        return (masks, breaks);
    }

    /// <summary>
    /// The bit of a level's flag in a candle's mask.
    /// </summary>
    public static int Bit(int level, EnumFlag flag) => (level - 1) * Flags + (int)flag;

    // What the last of these candles shows, read from them alone; first is where they begin in the series, which a break's
    // From counts from.
    private static (int Mask, IReadOnlyList<KnownBreak> Breaks) Seen(List<Price> prices, int first, EnumPriceBasis basis, int levels)
    {
        var last = prices[^1].DateTime;
        var sawtooth = Sawtooth.Levels(prices, basis, levels);
        var mask = 0;
        List<KnownBreak>? made = null;
        for (var level = 1; level <= levels && level < sawtooth.Count; level++)
        {
            var swings = Sawtooth.Swings(prices, sawtooth, basis, level);
            var breaks = Sawtooth.MarketStructureBreaks(prices, swings, basis);
            var trends = Sawtooth.Trends(swings, breaks);
            void Set(EnumFlag flag, bool on) => mask |= on ? 1 << Bit(level, flag) : 0;

            Set(EnumFlag.BreakUp, swings.Any(x => x.Direction == EnumSwingDirection.Up && x.BreakOfStructure?.Time == last));
            Set(EnumFlag.BreakDown, swings.Any(x => x.Direction == EnumSwingDirection.Down && x.BreakOfStructure?.Time == last));
            Set(EnumFlag.BullishMarketStructureBreak, breaks.Any(x => x.Type == EnumAnnotationType.BullishMarketStructureBreak && x.Break.Time == last));
            Set(EnumFlag.BearishMarketStructureBreak, breaks.Any(x => x.Type == EnumAnnotationType.BearishMarketStructureBreak && x.Break.Time == last));
            Set(EnumFlag.Uptrend, trends.Any(x => x.Direction == EnumSwingDirection.Up && x.End == null));
            Set(EnumFlag.Downtrend, trends.Any(x => x.Direction == EnumSwingDirection.Down && x.End == null));

            if (!swings.Any(x => x.BreakOfStructure?.Time == last) && !breaks.Any(x => x.Break.Time == last))
                continue;

            int At(DateTime time) => first + prices.FindIndex(x => x.DateTime == time);
            // The Swings in the order they were Confirmed, as a Trend counts them.
            var ordered = swings.Where(x => x.BreakOfStructure != null).OrderBy(x => x.BreakOfStructure!.Time).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var swing = ordered[i];
                if (swing.BreakOfStructure!.Time != last)
                    continue;
                var run = 1;
                while (i - run >= 0 && ordered[i - run].Direction == swing.Direction)
                    run++;
                var against = swing.Direction == EnumSwingDirection.Up ? EnumAnnotationType.BearishMarketStructureBreak : EnumAnnotationType.BullishMarketStructureBreak;
                var weak = breaks.Any(b => b.Type == against && b.Break.Time > swing.Start.Time && b.Break.Time < last);
                (made ??= []).Add(new KnownBreak(level, EnumKnownBreak.BreakOfStructure, swing.Direction, At(swing.Start.Time),
                    At(ordered[i - run + 1].Start.Time), run, weak));
            }

            foreach (var b in breaks.Where(x => x.Break.Time == last))
            {
                var direction = b.Type == EnumAnnotationType.BullishMarketStructureBreak ? EnumSwingDirection.Up : EnumSwingDirection.Down;
                var from = At(b.Reference.Time);
                (made ??= []).Add(new KnownBreak(level, EnumKnownBreak.MarketStructureBreak, direction, from, from));
            }
        }

        return (mask, made ?? None);
    }
}

public enum EnumKnownBreak
{
    BreakOfStructure,
    MarketStructureBreak,
}

/// <summary>
/// A break a level shows on a candle's close, with what it was read from. A break of structure Confirms a Swing: Direction
/// is the Swing's, From the candle the Swing begins at, Run how many Swings in a row that way it makes, as the chart of the
/// window counts them (two or more are a Trend), Begins the candle the first of those begins at, and Weak whether the Swing
/// has a market structure break inside it. A market structure break has the direction it breaks in, up for a bullish one,
/// and From and Begins are the candle of the Swing Low or Swing High it closes beyond.
/// </summary>
public sealed record KnownBreak(int Level, EnumKnownBreak Kind, EnumSwingDirection Direction, int From, int Begins, int Run = 0, bool Weak = false);
