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

    private static string Lines(IEnumerable<string> lines) => string.Join("\n", lines.Order(StringComparer.Ordinal));

    [TestMethod]
    public void TheRestKnownAtACandle_IsWhatTheSawtoothMakesOfTheCandlesToIt()
    {
        const EnumPriceBasis basis = EnumPriceBasis.Close;
        var prices = Btc.From2023().GetRange(0, 3000);
        var store = StructureStore.Read(prices);

        var found = 0;
        foreach (var at in new[] { 2, 57, 500, 501, 1234, 1900, 2000, 2650, 2998, 2999 })
        {
            var so = prices.GetRange(0, at + 1);
            var times = so.Select((p, i) => (p.DateTime, i)).ToDictionary(x => x.DateTime, x => x.i);
            int? At(PricePoint? point) => point == null ? null : times[point.Time];
            var levels = Sawtooth.Levels(so, basis, Depth);
            var swings = Enumerable.Range(0, levels.Count + 2).Select(l => Sawtooth.Swings(so, levels, basis, l).OrderBy(x => x.Start.Time).ToList()).ToList();
            var broken = swings.Select(x => Sawtooth.MarketStructureBreaks(so, x, basis)).ToList();
            var (breaks, trends, retracements, candidates, ranges) = (new List<string>(), new List<string>(), new List<string>(), new List<string>(), new List<string>());
            for (var level = 1; level <= levels.Count; level++)
            {
                var ofLevel = Sawtooth.Trends(swings[level], broken[level]);
                var candidatesOfLevel = Sawtooth.Candidates(so, levels, basis, level);
                var retracementsOfLevel = Sawtooth.Retracements(so, swings[level], candidatesOfLevel, basis);
                breaks.AddRange(broken[level].Select(x => $"L{level} {x.Type} {At(x.Reference)} {At(x.Break)}"));
                trends.AddRange(ofLevel.Select(x => $"L{level} {x.Direction} {At(x.Start)} {At(x.Confirmed)} {At(x.End)} {x.Swings} {x.MarketStructureBreaks} {x.Strong}"));
                retracements.AddRange(retracementsOfLevel.Select(x => $"L{level} {x.Direction} {At(x.From)} {At(x.To)} {x.Confirmed} " + string.Join(" ", x.Steps.Select(s => $"{At(s.Point)}:{s.Percent}"))));
                candidates.AddRange(candidatesOfLevel.Select(x => $"L{level} {x.Direction} {At(x.Start)} {At(x.Extreme)}"));
                ranges.AddRange(Ranges.Find(so, basis, retracementsOfLevel, swings[level], ofLevel, broken[level + 1])
                    .Select(x => $"L{level} {x.Direction} {At(x.From)} {At(x.Low)} {At(x.High)} {At(x.Identified)} {x.Retracement} {x.Band} {At(x.End)}"));
            }

            // Asked for to the last candle, what was known then is still only what lies up to that candle.
            Assert.AreEqual(Lines(breaks), Lines(store.Breaks(0, 2999, at).Select(x => $"L{x.Level} {x.Type} {x.Reference} {x.Break}")), $"The market structure breaks known at candle {at}.");
            Assert.AreEqual(Lines(trends), Lines(store.Trends(0, 2999, at).Select(x => $"L{x.Level} {x.Direction} {x.Start} {x.Confirmed} {x.End} {x.Swings} {x.MarketStructureBreaks} {x.Parts.Count(p => p.Strong)}")),
                $"The trends known at candle {at}.");
            Assert.AreEqual(Lines(retracements), Lines(store.Retracements(0, 2999, at).Select(x => $"L{x.Level} {x.Direction} {x.From} {x.To} {x.Confirmed} " + string.Join(" ", x.Steps.Select(s => $"{s.Candle}:{s.Percent}")))),
                $"The retracements known at candle {at}.");
            Assert.AreEqual(Lines(candidates), Lines(store.Candidates(at).Select(x => $"L{x.Level} {x.Direction} {x.Start} {x.Extreme}")), $"The candidates known at candle {at}.");
            Assert.AreEqual(Lines(ranges), Lines(store.Ranges(0, 2999, at).Select(x => $"L{x.Level} {x.Direction} {x.From} {x.Low} {x.High} {x.Identified} {x.Retracement} {x.Band} {x.End}")),
                $"The ranges known at candle {at}.");
            found += Math.Min(1, breaks.Count) + Math.Min(1, trends.Count) + Math.Min(1, retracements.Count) + Math.Min(1, candidates.Count) + Math.Min(1, ranges.Count);
        }

        Assert.IsTrue(found >= 40, $"Too little to find: {found} of 50.");
    }

    [TestMethod]
    public void AStretchAndALevelOfTheRest_GiveOnlyWhatIsInThem()
    {
        var store = StructureStore.Read(Btc.From2023().GetRange(0, 3000));
        var breaks = store.Breaks(0, 2999);
        var trends = store.Trends(0, 2999);
        var retracements = store.Retracements(0, 2999);
        var ranges = store.Ranges(0, 2999);

        Assert.AreEqual(string.Join(" ", breaks.Where(x => x.Break is >= 1000 and <= 1500 && x.Level <= 3).Select(x => x.Id)), string.Join(" ", store.Breaks(1000, 1500, maxLevel: 3).Select(x => x.Id)));
        Assert.AreEqual(string.Join(" ", trends.Where(x => x.Start <= 1500 && (x.End ?? 2999) >= 1000 && x.Level <= 3).Select(x => x.Id)), string.Join(" ", store.Trends(1000, 1500, maxLevel: 3).Select(x => x.Id)));
        Assert.AreEqual(string.Join(" ", retracements.Where(x => x.From <= 1500 && x.Steps[^1].Candle >= 1000 && x.Level <= 3).Select(x => x.Id)), string.Join(" ", store.Retracements(1000, 1500, maxLevel: 3).Select(x => x.Id)));
        Assert.AreEqual(string.Join(" ", ranges.Where(x => x.From <= 1500 && (x.End ?? 2999) >= 1000 && x.Level <= 5).Select(x => x.Id)), string.Join(" ", store.Ranges(1000, 1500, maxLevel: 5).Select(x => x.Id)));
        Assert.IsTrue(store.Breaks(1000, 1500, maxLevel: 3).Count > 0 && store.Trends(1000, 1500, maxLevel: 3).Count > 0 && store.Retracements(1000, 1500, maxLevel: 3).Count > 0);

        // Each break is of a swing the store has, and an id is of one item of its kind.
        var swings = store.Swings(0, 2999).ToDictionary(x => x.Id);
        Assert.IsTrue(breaks.All(x => swings[x.Swing].Extreme == x.Reference && swings[x.Swing].Level == x.Level));
        Assert.AreEqual(breaks.Count, breaks.Select(x => x.Id).Distinct().Count());
        Assert.AreEqual(trends.Count, trends.Select(x => x.Id).Distinct().Count());
    }

    [TestMethod]
    public void MoreCandles_LeaveTheRestThereWasItsIds()
    {
        var all = Btc.From2023();
        string Ids(StructureStore store) => string.Join(" ", store.Breaks(0, 599, at: 599).Select(x => $"b{x.Id}:{x.Break}")
            .Concat(store.Trends(0, 599, at: 599).Select(x => $"t{x.Id}:{x.Start}"))
            .Concat(store.Retracements(0, 599, at: 599).Select(x => $"r{x.Id}:{x.To}"))
            .Concat(store.Candidates(at: 599).Select(x => $"c{x.Id}:{x.Start}"))
            .Concat(store.Ranges(0, 599, at: 599).Select(x => $"g{x.Id}:{x.Identified}")));

        var ids = Ids(StructureStore.Read(all.GetRange(0, 600)));

        Assert.IsTrue(ids.Contains('b') && ids.Contains('t') && ids.Contains('r') && ids.Contains('c'), ids);
        Assert.AreEqual(ids, Ids(StructureStore.Read(all.GetRange(0, 700))));
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