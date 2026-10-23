namespace Gradient.CryptoAnalysis.Test.Structure;

/// <summary>
/// The market structure breaks that go further back than the last swing: after a run of swings one way, a close beyond
/// the turns of the last two or more of them.
/// </summary>
[TestClass]
public class DeepBreaksTests
{
    private static readonly Lazy<StructureStore> Hourly = new(() => StructureStore.Read(Btc.Hourly));

    private static int At(int year, int month, int day, int hour) => Btc.Hourly.FindIndex(x => x.DateTime == new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc));

    [TestMethod]
    public void Find_September2018_TheBounceTakesBackTwoThenSixOfTheElevenDownswings()
    {
        // Eleven Downswings to the low of 6,165 on 9 Sep 02:00. Their turns, the latest first: 6,200, 6,456, 6,512, 6,422,
        // 6,429, 6,445, then 6,948. The bounce closes above 6,456 on 13 Sep and above 6,512 on 14 Sep.
        var prices = Btc.Hourly;
        var far = At(2018, 9, 9, 2);

        var found = DeepBreaks.Find(Hourly.Value).Where(x => x.Far == far && x.Type == EnumAnnotationType.BullishMarketStructureBreak).ToList();

        CollectionAssert.AreEqual(new[] { (2, 11), (6, 11) }, found.Select(x => (x.Back, x.Of)).ToArray());
        var (two, six) = (found[0], found[1]);
        Assert.AreEqual((At(2018, 9, 13, 11), At(2018, 9, 8, 9), 6456), (two.Break, two.Reference, (int)prices[two.Reference].Close));
        Assert.AreEqual((At(2018, 9, 14, 2), At(2018, 9, 7, 1), 6512, At(2018, 9, 6, 0)), (six.Break, six.Reference, (int)prices[six.Reference].Close, six.From));
        Assert.IsTrue(prices[six.Break].Close > 6512 && prices[six.Break].Close < 6948, "Above the sixth turn back, below the seventh.");
    }

    [TestMethod]
    public void Find_EachOne_ClosesBeyondItsTurnForTheFirstTimeSinceTheCounterMoveBegan()
    {
        var prices = Btc.Hourly;
        var found = DeepBreaks.Find(Hourly.Value);

        Assert.IsTrue(found.Count > 1000, $"{found.Count} found");
        foreach (var deep in found)
        {
            var bullish = deep.Type == EnumAnnotationType.BullishMarketStructureBreak;
            var (turn, close) = (prices[deep.Reference].Close, prices[deep.Break].Close);
            Assert.IsTrue(deep.Back >= 2 && deep.Back <= deep.Of && deep.From < deep.Far && deep.Reference < deep.Far && deep.Far < deep.Break, $"{deep}");
            Assert.IsTrue(bullish ? close > turn : close < turn, $"{deep}");
            for (var i = deep.Far; i < deep.Break; i++)
                Assert.IsFalse(bullish ? prices[i].Close > turn : prices[i].Close < turn, $"{deep}: #{i} is beyond the turn already");
        }
    }

    [TestMethod]
    public void Find_IsFoundFromTheCandlesUpToItAlone()
    {
        var cut = At(2018, 9, 13, 20);

        var before = DeepBreaks.Find(StructureStore.Read(Btc.Hourly.GetRange(0, cut + 1)));

        CollectionAssert.AreEqual(DeepBreaks.Find(Hourly.Value).Where(x => x.Break <= cut).ToList(), before.ToList());
    }

    [TestMethod]
    public void Timeline_WhenAskedFor_HasThemAmongItsMarketStructureBreaks()
    {
        var store = Hourly.Value;
        var (from, to) = (At(2018, 9, 5, 0), At(2018, 9, 18, 0));

        var without = MarketStructure.Complete(store, 8, from, to).MarketStructureBreaks.Select(x => x.Value).ToList();
        var with = MarketStructure.Complete(store, 8, from, to, deepBreaks: true).MarketStructureBreaks.Select(x => x.Value).ToList();

        Assert.IsTrue(without.All(x => x.Break.Back == 1 && x.Break.From == null));
        CollectionAssert.AreEqual(without, with.Where(x => x.Break.Back == 1).ToList());
        var six = with.Single(x => x.Break.Back == 6 && x.Break.Break.Time == new DateTime(2018, 9, 14, 2, 0, 0)).Break;
        Assert.AreEqual((11, 6512, 6165, new DateTime(2018, 9, 6, 0, 0, 0)), (six.Of, (int)six.Reference.Price, (int)six.Far!.Price, six.From!.Time));
    }
}
