namespace Gradient.CryptoAnalysis.Test.Structure;

/// <summary>
/// The ranges found by swings: the price inside the last leg of a level, between its two ends, while the level after it
/// confirms a count of swings.
/// </summary>
[TestClass]
public class SwingRangesTests
{
    private static readonly Lazy<StructureStore> Hourly = new(() => StructureStore.Read(Btc.Hourly));

    private static int At(int year, int month, int day, int hour) => Btc.Hourly.FindIndex(x => x.DateTime == new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc));

    [TestMethod]
    public void Find_TheRangeOfFebruary2025_IsTheLegFromItsLowToItsHighUntilTheCloseBelowTheLow()
    {
        // The low of 92,841 on 3 Feb 04:00, the high of 102,094 that day at 19:00, and three weeks between them.
        var range = SwingRanges.Find(Hourly.Value).Single(x => x.Leg == At(2025, 2, 3, 19));

        var prices = Btc.Hourly;
        Assert.AreEqual((EnumPivotKind.High, 102094, 92841), (range.LegKind, (int)prices[range.Leg].Close, (int)prices[range.Other].Close));
        Assert.AreEqual((4, 5, At(2025, 2, 3, 4), At(2025, 2, 4, 21), At(2025, 2, 24, 22)), (range.Level, range.Inner, range.Other, range.Identified, range.End));
        Assert.AreEqual(SwingRanges.DefaultSwings, range.Swings, "Identified at the break of structure that makes the count.");
        Assert.IsTrue(prices[range.End!.Value].Close < 92841 && Enumerable.Range(range.Identified, range.End.Value - range.Identified).All(i => prices[i].Close is > 92841 and < 102094));
    }

    [TestMethod]
    public void Find_EachRange_HoldsBetweenItsEndsFromTheCandleThatIdentifiesItToTheCloseBeyondOne()
    {
        var prices = Btc.Hourly;
        var found = SwingRanges.Find(Hourly.Value);

        Assert.IsTrue(found.Count > 1000, $"{found.Count} found");
        foreach (var range in found)
        {
            var (high, low) = range.LegKind == EnumPivotKind.High ? (prices[range.Leg].Close, prices[range.Other].Close) : (prices[range.Other].Close, prices[range.Leg].Close);
            Assert.IsTrue(range.Other < range.Leg && range.Leg < range.Identified && high > low && range.Swings >= SwingRanges.DefaultSwings && range.Inner == range.Level + 1, $"{range}");
            for (var i = range.Identified; i < (range.End ?? prices.Count); i++)
                Assert.IsTrue(prices[i].Close < high && prices[i].Close > low, $"{range} at #{i}");
            if (range.End is int end)
                Assert.IsTrue(prices[end].Close >= high || prices[end].Close <= low, $"{range}");
        }
    }

    [TestMethod]
    public void Find_ARange_IsFoundFromTheCandlesUpToItAlone()
    {
        var all = SwingRanges.Find(Hourly.Value);
        var cut = At(2025, 2, 10, 0);
        var before = SwingRanges.Find(StructureStore.Read(Btc.Hourly.GetRange(0, cut + 1)));

        // What a shorter history finds is what the whole finds by then, each still holding where its end came later.
        CollectionAssert.AreEqual(all.Where(x => x.Identified <= cut).Select(x => x with { End = x.End <= cut ? x.End : null }).ToList(), before.ToList());
    }

    [TestMethod]
    public void Over_AsKnownAtACandle_HasTheRangeWithTheSwingsCountedByThenAndNoEndBeforeItCame()
    {
        var store = Hourly.Value;
        var leg = At(2025, 2, 3, 19);

        Assert.AreEqual(0, SwingRanges.Over(store, 4, leg, At(2025, 2, 4, 20), At(2025, 2, 4, 20)).Count(x => x.Leg == leg), "Not before the fourth Swing.");
        var early = SwingRanges.Over(store, 4, leg, At(2025, 2, 5, 0), At(2025, 2, 5, 0)).Single(x => x.Leg == leg);
        var late = SwingRanges.Over(store, 4, leg, At(2025, 2, 20, 0), At(2025, 2, 20, 0)).Single(x => x.Leg == leg);
        var after = SwingRanges.Over(store, 4, leg, At(2025, 3, 1, 0), At(2025, 3, 1, 0)).Single(x => x.Leg == leg);

        Assert.AreEqual((4, null), (early.Swings, early.End));
        Assert.IsTrue(late.Swings > early.Swings && late.Weak >= 2 && late.End == null, $"{late}");
        Assert.AreEqual(At(2025, 2, 24, 22), after.End);
    }

    [TestMethod]
    public void Timeline_WithACountOfSwings_HasTheRangesFoundBySwingsAmongItsRanges()
    {
        var store = Hourly.Value;
        var (from, to) = (At(2025, 2, 1, 0), At(2025, 2, 26, 0));

        var without = MarketStructure.Complete(store, 8, from, to).Ranges.Select(x => x.Value).ToList();
        var with = MarketStructure.Complete(store, 8, from, to, rangeSwings: 4).Ranges.Select(x => x.Value).ToList();

        Assert.IsTrue(without.All(x => x.Swings == 0));
        CollectionAssert.AreEqual(without, with.Where(x => x.Swings == 0).ToList());
        var range = with.Single(x => x.Swings > 0 && (int)x.High.Price == 102094 && (int)x.Low.Price == 92841);
        Assert.AreEqual((new DateTime(2025, 2, 4, 21, 0, 0), new DateTime(2025, 2, 24, 22, 0, 0), 0.0), (range.Identified.Time, range.End!.Time, range.Band));
        Assert.IsTrue(range.End.Price < range.Low.Price && !range.EndedAbove);

        // The Swings it was found by are with it: those one level finer inside its leg, every one Confirmed inside it.
        Assert.AreEqual(range.Swings, range.Inner.Count);
        Assert.IsTrue(range.Inner.All(x => x.Level == range.Level + 1 && x.Direction == EnumSwingDirection.Down && x.Start.Time >= new DateTime(2025, 2, 3, 19, 0, 0)
            && x.BreakOfStructure!.Time <= range.End.Time), string.Join("; ", range.Inner));
        Assert.AreEqual(with.Single(x => x.Swings > 0 && (int)x.High.Price == 102094 && (int)x.Low.Price == 92841), range, "Compared by value, its Swings included.");
    }
}
