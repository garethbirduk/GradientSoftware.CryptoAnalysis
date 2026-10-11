using Gradient.CryptoAnalysis.Site;

namespace Gradient.CryptoAnalysis.Test.Site;

[TestClass]
public class StructureStoreTests
{
    // More levels than the stretch has, so every level is read.
    private const int Depth = 60;

    /// <summary>
    /// What the sawtooth makes of the candles to one: each pivot that can no longer move at the first level it is at, and
    /// each confirmed swing at its level.
    /// </summary>
    private static (string Pivots, string Swings) Expected(List<Price> prices, int at)
    {
        var so = prices.GetRange(0, at + 1);
        var levels = Sawtooth.Levels(so, EnumPriceBasis.Close, Depth);
        var times = so.Select((p, i) => (p.DateTime, i)).ToDictionary(x => x.DateTime, x => x.i);
        var pivots = levels.SelectMany(l => l.Pivots.Where(p => p.Kind is EnumPivotKind.High or EnumPivotKind.Low && p.ConfirmedIndex != null).Select(p => (l.Level, Pivot: p)))
            .GroupBy(x => (x.Pivot.Index, x.Pivot.Kind)).Select(g => g.First()).OrderBy(x => x.Pivot.Index).ThenBy(x => x.Pivot.Kind)
            .Select(x => $"{x.Pivot.Index} {x.Pivot.Kind} {x.Pivot.Price} L{x.Level}");
        var swings = Enumerable.Range(1, levels.Count).SelectMany(l => Sawtooth.Swings(so, levels, EnumPriceBasis.Close, l))
            .Select(x => $"{x.Direction} {times[x.Start.Time]} {times[x.Extreme.Time]} {times[x.BreakOfStructure!.Time]} L{x.Level}").Order();
        return (string.Join("\n", pivots), string.Join("\n", swings));
    }

    private static string Describe(IEnumerable<StoredPivot> pivots) => string.Join("\n", pivots.Select(x => $"{x.Candle} {x.Kind} {x.Price} L{x.Level}"));

    private static string Describe(IEnumerable<StoredSwing> swings) => string.Join("\n", swings.Select(x => $"{x.Direction} {x.Start} {x.Extreme} {x.Break} L{x.Level}").Order());

    [TestMethod]
    public void WhatWasKnownAtACandle_IsWhatTheSawtoothMakesOfTheCandlesToIt()
    {
        var prices = Btc.From2023().GetRange(0, 3000);
        var store = StructureStore.Read(prices);

        Assert.AreEqual(3000, store.Candles);
        foreach (var at in new[] { 1, 2, 57, 500, 501, 1234, 2000, 2998, 2999 })
        {
            var (pivots, swings) = Expected(prices, at);
            Assert.AreEqual(pivots, Describe(store.Pivots(0, at, at)), $"The pivots known at candle {at}.");
            Assert.AreEqual(swings, Describe(store.Swings(0, at, at)), $"The swings known at candle {at}.");
            // Asked for to the last candle, what was known then is still only what lies up to that candle.
            Assert.AreEqual(pivots, Describe(store.Pivots(0, 2999, at)), $"The pivots known at candle {at}, of every candle.");
            Assert.AreEqual(swings, Describe(store.Swings(0, 2999, at)), $"The swings known at candle {at}, of every candle.");
        }

        var (lastPivots, lastSwings) = Expected(prices, 2999);
        Assert.AreEqual(lastPivots, Describe(store.Pivots(0, 2999)), "With no candle given, as known at the last.");
        Assert.AreEqual(lastSwings, Describe(store.Swings(0, 2999)));
    }

    [TestMethod]
    public void AStretchAndALevel_GiveOnlyWhatIsInThem()
    {
        var store = StructureStore.Read(Btc.From2023().GetRange(0, 3000));
        var all = store.Pivots(0, 2999);
        var allSwings = store.Swings(0, 2999);

        Assert.AreEqual(Describe(all.Where(x => x.Candle is >= 1000 and <= 1500 && x.Level <= 3)), Describe(store.Pivots(1000, 1500, maxLevel: 3)));
        Assert.AreEqual(Describe(allSwings.Where(x => x.Break >= 1000 && x.Start <= 1500 && x.Level <= 3)), Describe(store.Swings(1000, 1500, maxLevel: 3)));
        Assert.IsTrue(store.Pivots(1000, 1500, maxLevel: 3).Count > 0 && store.Swings(1000, 1500, maxLevel: 3).Count > 0);

        // A pivot's id is its candle and kind, and each is known from a candle after its own.
        Assert.IsTrue(all.All(x => x.Id == StructureStore.PivotId(x.Candle, x.Kind) && x.Known > x.Candle));
        Assert.AreEqual(allSwings.Count, allSwings.Select(x => x.Id).Distinct().Count());
    }

    [TestMethod]
    public void MoreCandles_LeaveTheSwingsThereWereTheirIds()
    {
        var all = Btc.From2023();
        string Ids(StructureStore store) => string.Join(" ", store.Swings(0, 599, at: 599).Select(x => $"{x.Id}:{x.Break}"));

        var ids = Ids(StructureStore.Read(all.GetRange(0, 600)));

        Assert.AreNotEqual("", ids);
        Assert.AreEqual(ids, Ids(StructureStore.Read(all.GetRange(0, 700))));
    }
}