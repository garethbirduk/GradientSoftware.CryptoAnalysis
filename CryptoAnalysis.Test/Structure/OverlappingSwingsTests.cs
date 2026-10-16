namespace Gradient.CryptoAnalysis.Test.Structure;

/// <summary>
/// The Swings against the leg of the level above, which the search inside each leg leaves out: those that run from one
/// of its legs into the next (across), and those inside the one leg.
/// </summary>
[TestClass]
public class OverlappingSwingsTests
{
    private static readonly Lazy<StructureStore> Hourly = new(() => StructureStore.Read(Btc.Hourly));

    private static int At(int year, int month, int day, int hour) => Btc.Hourly.FindIndex(x => x.DateTime == new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc));

    private static List<SwingOutline> Overlapping(int from, int to) =>
        MarketStructure.Complete(Hourly.Value, from: from, to: to, overlaps: true).Swings.Select(x => x.Value).Where(x => x.Overlap != null).ToList();

    [TestMethod]
    public void Complete_September2019_TheUpswingFromTheLastLowerHighBeforeTheLowRunsAcrossIntoTheNextLeg()
    {
        // Level 5: a Downswing to the LL of 10142 on 9 Sep 08:00, the next one starting there. The last level 6 Swing High
        // before the LL is 10325 at 04:00, and the close of 10430 at 11:00 is the first above it.
        var swings = Overlapping(At(2019, 9, 8, 0), At(2019, 9, 10, 0));

        var across = swings.Single(x => x.Start.Time == Btc.Hourly[At(2019, 9, 9, 4)].DateTime && x.Direction == EnumSwingDirection.Up);

        Assert.AreEqual(("across", 6, Btc.Hourly[At(2019, 9, 9, 8)].DateTime, Btc.Hourly[At(2019, 9, 9, 11)].DateTime),
            (across.Overlap, across.Level, across.Extreme.Time, across.BreakOfStructure!.Time));
    }

    [TestMethod]
    public void Complete_September2019_TheUpswingInsideTheLevel4DownlegLiesInside()
    {
        // Level 4 falls from 10891 on 6 Sep 16:00 to 24 Sep. Inside it, level 5 has a High of 10424 at 21:00 and a Low of
        // 10308 at 23:00, and the close of 10428 at 11:00 on the 7th is the first above 10424.
        var swings = Overlapping(At(2019, 9, 6, 12), At(2019, 9, 8, 0));

        var inside = swings.Single(x => x.Start.Time == Btc.Hourly[At(2019, 9, 6, 21)].DateTime && x.Level == 5);

        Assert.AreEqual(("inside", EnumSwingDirection.Up, Btc.Hourly[At(2019, 9, 6, 23)].DateTime, Btc.Hourly[At(2019, 9, 7, 11)].DateTime),
            (inside.Overlap, inside.Direction, inside.Extreme.Time, inside.BreakOfStructure!.Time));
    }

    [TestMethod]
    public void Complete_WithoutOverlaps_HasNone()
    {
        var swings = MarketStructure.Complete(Hourly.Value, from: At(2019, 9, 6, 12), to: At(2019, 9, 10, 0)).Swings.Select(x => x.Value);

        Assert.IsFalse(swings.Any(x => x.Overlap != null));
    }
}
