using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Gradient.CryptoAnalysis.Research;

/// <summary>
/// What market structure showed at each candle's close, as it was known then: looked up in the structure store of the
/// series (see <see cref="StructureStore"/>), which reads every candle from the first once and holds how each thing stood
/// from candle to candle. So it is what could have been known at the candle, and the same structure the pages draw. A
/// level is the store's at that candle: level 1 is the structure of all the candles to it and each level after it finer,
/// and a thing's level can be another at a later candle.
/// </summary>
public static class KnownStructure
{
    /// <summary>
    /// The finest level a step or a condition can name.
    /// </summary>
    public const int Levels = 16;

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

    private static readonly IReadOnlyList<KnownBreak> None = [];

    // The breaks each candle's close made, and the levels a trend held at there each way, a bit a level.
    private sealed record Known(IReadOnlyList<KnownBreak>[] Breaks, int[] Uptrends, int[] Downtrends);

    // Each series' breaks and trends once, kept while the series is.
    private static readonly ConditionalWeakTable<IReadOnlyList<Price>, Lazy<Known>> Kept = [];

    private static Known Of(IReadOnlyList<Price> prices) =>
        Kept.GetValue(prices, p => new Lazy<Known>(() => Of(Store(p, Ranges.DefaultMinRetracement, Ranges.DefaultBand)))).Value;

    /// <summary>
    /// Where the structure store of a series is had from when something holds one already, as the server does its
    /// datasets': given the series and the range settings, the least retracement and the band. Null, or a null answer,
    /// to read the series here.
    /// </summary>
    public static Func<IReadOnlyList<Price>, double, double, StructureStore?>? Stores { get; set; }

    // The stores read here, each for as long as something still has it: what is worked out of one is kept, not the store.
    private static readonly ConditionalWeakTable<IReadOnlyList<Price>, ConcurrentDictionary<(double, double), WeakReference<StructureStore>>> Read = [];

    /// <summary>
    /// The structure store of a series on its closes, with ranges of a least retracement and a band: the one held for it
    /// (see <see cref="Stores"/>), or else read from its candles.
    /// </summary>
    public static StructureStore Store(IReadOnlyList<Price> prices, double minRetracement, double band)
    {
        if (Stores?.Invoke(prices, minRetracement, band) is { } held)
            return held;

        var read = Read.GetValue(prices, _ => new());
        lock (read)
        {
            if (read.TryGetValue((minRetracement, band), out var had) && had.TryGetTarget(out var store))
                return store;
            store = StructureStore.Read(prices as List<Price> ?? prices.ToList(), EnumPriceBasis.Close, minRetracement, band);
            read[(minRetracement, band)] = new WeakReference<StructureStore>(store);
            return store;
        }
    }

    /// <summary>
    /// The candles at which a level shows a flag, as known at each; worked out once a series and kept.
    /// </summary>
    public static bool[] Met(IReadOnlyList<Price> prices, int level, EnumFlag flag)
    {
        var known = Of(prices);
        if (flag is EnumFlag.Uptrend or EnumFlag.Downtrend)
        {
            var bit = level is >= 1 and <= Levels ? 1 << (level - 1) : 0;
            return (flag == EnumFlag.Uptrend ? known.Uptrends : known.Downtrends).Select(x => (x & bit) != 0).ToArray();
        }

        var kind = flag is EnumFlag.BreakUp or EnumFlag.BreakDown ? EnumKnownBreak.BreakOfStructure : EnumKnownBreak.MarketStructureBreak;
        var direction = flag is EnumFlag.BreakUp or EnumFlag.BullishMarketStructureBreak ? EnumSwingDirection.Up : EnumSwingDirection.Down;
        return known.Breaks.Select(x => x.Any(b => b.Level == level && b.Kind == kind && b.Direction == direction)).ToArray();
    }

    /// <summary>
    /// The breaks each candle's close made, at every level, each with what it was read from (see <see cref="KnownBreak"/>);
    /// worked out once a series and kept.
    /// </summary>
    public static IReadOnlyList<KnownBreak>[] Breaks(IReadOnlyList<Price> prices) => Of(prices).Breaks;

    // What each candle of a store's shows, as known at it.
    private static Known Of(StructureStore store)
    {
        var breaks = new IReadOnlyList<KnownBreak>[store.Candles];
        var (uptrends, downtrends) = (new int[store.Candles], new int[store.Candles]);

        // A trend's count and where it begins, at each break of structure that adds a swing to it.
        var runs = new Dictionary<(int At, int Level, EnumSwingDirection Direction), (int Run, int Begins)>();
        foreach (var (trend, from, to) in store.TrendStretches())
        {
            if (trend.Parts.Count > 0 && trend.Parts[^1].Break == from)
                runs[(from, trend.Level, trend.Direction)] = (trend.Swings, trend.Start);
            if (trend.End != null || trend.Level > Levels)
                continue;
            var (held, bit) = (trend.Direction == EnumSwingDirection.Up ? uptrends : downtrends, 1 << (trend.Level - 1));
            for (var t = from; t <= to; t++)
                held[t] |= bit;
        }

        for (var t = 0; t < store.Candles; t++)
        {
            List<KnownBreak>? made = null;
            // The swings whose break of structure is this candle's close.
            foreach (var swing in store.Swings(t, t, t))
            {
                var against = swing.Direction == EnumSwingDirection.Up ? EnumAnnotationType.BearishMarketStructureBreak : EnumAnnotationType.BullishMarketStructureBreak;
                var weak = store.Breaks(swing.Start + 1, t - 1, t).Any(x => x.Level == swing.Level && x.Type == against);
                var (run, begins) = runs.TryGetValue((t, swing.Level, swing.Direction), out var counted) ? counted : (1, swing.Start);
                (made ??= []).Add(new KnownBreak(swing.Level, EnumKnownBreak.BreakOfStructure, swing.Direction, swing.Start, begins, run, weak));
            }

            foreach (var b in store.Breaks(t, t, t))
            {
                var direction = b.Type == EnumAnnotationType.BullishMarketStructureBreak ? EnumSwingDirection.Up : EnumSwingDirection.Down;
                (made ??= []).Add(new KnownBreak(b.Level, EnumKnownBreak.MarketStructureBreak, direction, b.Reference, b.Reference));
            }

            breaks[t] = made?.OrderBy(x => x.Level).ThenBy(x => x.Kind).ToList() ?? None;
        }

        return new Known(breaks, uptrends, downtrends);
    }
}

public enum EnumKnownBreak
{
    BreakOfStructure,
    MarketStructureBreak,
}

/// <summary>
/// A break a level shows on a candle's close, with what it was read from. A break of structure Confirms a Swing: Direction
/// is the Swing's, From the candle the Swing begins at, Run how many Swings in a row that way it makes, as known at that
/// candle (two or more are a Trend), Begins the candle the first of those begins at, and Weak whether the Swing has a
/// market structure break inside it. A market structure break has the direction it breaks in, up for a bullish one, and
/// From and Begins are the candle of the Swing Low or Swing High it closes beyond.
/// </summary>
public sealed record KnownBreak(int Level, EnumKnownBreak Kind, EnumSwingDirection Direction, int From, int Begins, int Run = 0, bool Weak = false);
