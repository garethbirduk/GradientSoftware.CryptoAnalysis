using System.Runtime.CompilerServices;

namespace Gradient.CryptoAnalysis.Site;

/// <summary>
/// The structure stores held for the series being read, found by a series' candles: what a tour or an analysis reads of
/// the market structure at a candle is then looked up in the store of its series and not worked out from every candle up
/// to it. A series is told by its first candle, and the candles asked about by their last, so the first candles of a
/// series, as a tour's reading "the chart as it was at a candle" is, find the store of the whole.
/// A store is held for the closes of a series from its first candle on, one for a series, and what was known at a candle
/// does not depend on the range settings it was read with. With no store held, everything is worked out as before.
/// </summary>
public static class StructureStores
{
    private static readonly ConditionalWeakTable<Price, StructureStore> Held = [];
    private static readonly ConditionalWeakTable<StructureStore, Lazy<Dictionary<DateTime, int>>> Candles = [];

    /// <summary>
    /// Holds a store for the series its candles begin, in place of any held for it before.
    /// </summary>
    public static void Hold(StructureStore store)
    {
        if (store.Candles > 0 && store.Basis == EnumPriceBasis.Close)
            Held.AddOrUpdate(store.Prices[0], store);
    }

    /// <summary>
    /// Stops holding a store.
    /// </summary>
    public static void Drop(StructureStore store)
    {
        if (store.Candles > 0 && Held.TryGetValue(store.Prices[0], out var held) && ReferenceEquals(held, store))
            Held.Remove(store.Prices[0]);
    }

    /// <summary>
    /// The store held for the series these candles are the first of, when one is that has them all: what is known at the
    /// last of them is what the store has as known at its candle of that number.
    /// </summary>
    public static StructureStore? Of(IReadOnlyList<Price> prices) =>
        prices.Count > 0 && Held.TryGetValue(prices[0], out var store) && store.Candles >= prices.Count && ReferenceEquals(store.Prices[prices.Count - 1], prices[^1])
            ? store
            : null;

    /// <summary>
    /// The candle of each time in a store's series, the first when a time comes twice; made once for a store.
    /// </summary>
    public static Dictionary<DateTime, int> IndexOf(StructureStore store) => Candles.GetValue(store, s => new Lazy<Dictionary<DateTime, int>>(() =>
    {
        var index = new Dictionary<DateTime, int>(s.Candles);
        for (var i = 0; i < s.Candles; i++)
            index.TryAdd(s.Prices[i].DateTime, i);
        return index;
    })).Value;
}
