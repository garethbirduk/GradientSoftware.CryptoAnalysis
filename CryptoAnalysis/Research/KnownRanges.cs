using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Gradient.CryptoAnalysis.Research;

/// <summary>
/// A Range a level shows holding at a candle's close (see <see cref="Ranges"/>), as its lines: Low and High, with Band how
/// far beyond them, as a percentage of its height, a close ends it. From is the candle its Retracement is measured from,
/// Identified the candle whose close identified it, and Retracement the reading there. The lines are fixed once it is
/// identified, so what holds a Range from then on is the price alone.
/// </summary>
public sealed record KnownRange(int Level, EnumSwingDirection Direction, double Low, double High, double Band, int Retracement, int From, int Identified)
{
    public double Height => High - Low;

    public double BandLow => Low - Height * Band / 100;

    public double BandHigh => High + Height * Band / 100;

    /// <summary>
    /// The top of the buy-low zone: the given share of the height above the low.
    /// </summary>
    public double BuyLowTop(double depth) => Low + Height * depth / 100;

    /// <summary>
    /// The bottom of the sell-high zone: the given share of the height below the high.
    /// </summary>
    public double SellHighBottom(double depth) => High - Height * depth / 100;

    /// <summary>
    /// Whether a close ends the Range: one beyond either band.
    /// </summary>
    public bool EndedBy(double close) => close > BandHigh || close < BandLow;
}

/// <summary>
/// The Ranges holding at each candle's close, as known then: looked up in the structure store of the series, as
/// <see cref="KnownStructure"/> is, so a level is the store's at that candle.
/// </summary>
public static class KnownRanges
{
    private static readonly IReadOnlyList<KnownRange> None = [];

    // Each series' Ranges once for each pair of settings, kept while the series is.
    private static readonly ConditionalWeakTable<IReadOnlyList<Price>, ConcurrentDictionary<(double, double), Lazy<IReadOnlyList<KnownRange>[]>>> Known = [];

    /// <summary>
    /// The Ranges holding at each candle's close, at every level, for a least Retracement and a band: at a level, the one
    /// identified last. Worked out once a series and pair of settings and kept.
    /// </summary>
    public static IReadOnlyList<KnownRange>[] Holding(IReadOnlyList<Price> prices, double minRetracement = Ranges.DefaultMinRetracement, double band = Ranges.DefaultBand) =>
        Known.GetValue(prices, _ => new()).GetOrAdd((minRetracement, band),
            key => new Lazy<IReadOnlyList<KnownRange>[]>(() => Of(KnownStructure.Store(prices, key.Item1, key.Item2)))).Value;

    // The Ranges each candle of a store's shows holding, as known at it.
    private static IReadOnlyList<KnownRange>[] Of(StructureStore store)
    {
        var held = new List<KnownRange>?[store.Candles];
        foreach (var (range, from, to) in store.RangeStretches())
        {
            if (range.End != null)
                continue;
            var known = new KnownRange(range.Level, range.Direction, store.Price(range.Low, high: false), store.Price(range.High, high: true), range.Band,
                range.Retracement, range.From, range.Identified);
            for (var t = Math.Max(from, range.Identified); t <= to; t++)
            {
                var at = held[t] ??= [];
                var same = at.FindIndex(x => x.Level == known.Level);
                if (same < 0)
                    at.Add(known);
                else if (at[same].Identified <= known.Identified)
                    at[same] = known;
            }
        }

        return held.Select(x => (IReadOnlyList<KnownRange>?)x ?? None).ToArray();
    }
}
