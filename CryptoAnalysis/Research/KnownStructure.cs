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

    // Each series' flags once, kept while the series is.
    private static readonly ConditionalWeakTable<IReadOnlyList<Price>, Lazy<int[]>> Known = [];

    /// <summary>
    /// The candles at which a level shows a flag, worked out once a series and kept.
    /// </summary>
    public static bool[] Met(IReadOnlyList<Price> prices, int level, EnumFlag flag)
    {
        var masks = Known.GetValue(prices, p => new Lazy<int[]>(() => Masks(p, EnumPriceBasis.Close, Window, Levels))).Value;
        var bit = 1 << Bit(level, flag);
        return masks.Select(x => (x & bit) != 0).ToArray();
    }

    /// <summary>
    /// Each candle's flags at levels 1 to levels, a bit a level and flag (see Bit), each from the window of candles to it.
    /// </summary>
    public static int[] Masks(IReadOnlyList<Price> prices, EnumPriceBasis basis, int window, int levels)
    {
        var list = prices as List<Price> ?? prices.ToList();
        var masks = new int[list.Count];
        Parallel.For(0, list.Count, t =>
        {
            var from = Math.Max(0, t + 1 - window);
            masks[t] = Mask(list.GetRange(from, t + 1 - from), basis, levels);
        });
        return masks;
    }

    /// <summary>
    /// The bit of a level's flag in a candle's mask.
    /// </summary>
    public static int Bit(int level, EnumFlag flag) => (level - 1) * Flags + (int)flag;

    private static int Mask(List<Price> prices, EnumPriceBasis basis, int levels)
    {
        var last = prices[^1].DateTime;
        var sawtooth = Sawtooth.Levels(prices, basis, levels);
        var mask = 0;
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
        }

        return mask;
    }
}
