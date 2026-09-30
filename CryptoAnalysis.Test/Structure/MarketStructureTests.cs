using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Conditions;
using Gradient.CryptoAnalysis.Conditions.PriceConditions;
using Gradient.CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis.Test.Structure;

[TestClass]
public class MarketStructureTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<Price> Closes(params double[] closes) =>
        closes.Select((c, i) => new Price { DateTime = Start.AddHours(i), Open = c, High = c, Low = c, Close = c }).ToList();

    private static string Describe(IEnumerable<StructureEvent> events) =>
        string.Join(" ", events.Select(e => $"{e.Type}@{(e.Time - Start).TotalHours}"));

    [TestMethod]
    public void Replay_PullbackIsThePresumedDeathLegUntilTheNewHigh()
    {
        var prices = Closes(10, 20, 17, 18, 15, 16, 14, 21);

        var live = MarketStructure.Replay(prices, EnumPriceBasis.Close, 1);
        var hindsight = TermAnnotations.Annotate(prices, EnumCloseType.Close, 1)
            .Where(x => x.Type is EnumAnnotationType.BullishBreakOfStructure or EnumAnnotationType.BearishBreakOfStructure or EnumAnnotationType.Downtrend);

        Assert.AreEqual("BearishBreakOfStructure@4 BearishBreakOfStructure@6 Downtrend@6 BullishBreakOfStructure@7", Describe(live));
        Assert.AreEqual("BullishBreakOfStructure@7", string.Join(" ", hindsight.Select(x => $"{x.Type}@{(x.Time - Start).TotalHours}")));
    }

    [TestMethod]
    public void EventsOnLastCandle_OnlyWhatHappensOnIt()
    {
        var prices = Closes(10, 12, 11, 13, 12, 15);

        Assert.AreEqual("BullishBreakOfStructure@5 Uptrend@5", Describe(MarketStructure.EventsOnLastCandle(prices, EnumPriceBasis.Close, 1)));
        Assert.AreEqual("", Describe(MarketStructure.EventsOnLastCandle(prices.Take(5).ToList(), EnumPriceBasis.Close, 1)));
    }

    [TestMethod]
    public void Replay_Window_SeesOnlyTheLastCandles()
    {
        var prices = Closes(10, 12, 11, 13, 12, 15);

        Assert.AreEqual("BullishBreakOfStructure@3 BullishBreakOfStructure@5 Uptrend@5", Describe(MarketStructure.Replay(prices, EnumPriceBasis.Close, 1)));
        Assert.AreEqual("", Describe(MarketStructure.Replay(prices, EnumPriceBasis.Close, 1, window: 2)));
    }

    [TestMethod]
    public void IsUptrendCondition_HoldsFromConfirmationUntilADownswingBreaks()
    {
        var prices = Closes(10, 12, 11, 13, 12, 15, 14, 13, 13.5, 12.5, 13, 12);
        var up = new ConditionSet();
        up.AndConditions.Add(new IsUptrendCondition());
        var down = new ConditionSet();
        down.AndConditions.Add(new IsTrendCondition(EnumSwingDirection.Down));

        Assert.AreEqual("5 6 7 8", string.Join(" ", prices.Where(p => up.IsMet(prices, p.DateTime)).Select(p => (p.DateTime - Start).TotalHours)));
        Assert.AreEqual("11", string.Join(" ", prices.Where(p => down.IsMet(prices, p.DateTime)).Select(p => (p.DateTime - Start).TotalHours)));
    }

    [TestMethod]
    public void IsTrendCondition_MaxMarketStructureBreaks()
    {
        var prices = Closes(10, 12, 11, 13, 10.5, 14);
        var strict = new ConditionSet();
        strict.AndConditions.Add(new IsUptrendCondition(maxMarketStructureBreaks: 0));
        var loose = new ConditionSet();
        loose.AndConditions.Add(new IsUptrendCondition(maxMarketStructureBreaks: 1));

        Assert.IsFalse(strict.IsMet(prices, prices[^1].DateTime));
        Assert.IsTrue(loose.IsMet(prices, prices[^1].DateTime));
    }

    [TestMethod]
    public void IsBreakOfStructureCondition_Bearish()
    {
        var prices = Closes(20, 18, 19, 17);
        var condition = new ConditionSet();
        condition.AndConditions.Add(new IsBreakOfStructureCondition(direction: EnumSwingDirection.Down));

        Assert.AreEqual("3", string.Join(" ", prices.Where(p => condition.IsMet(prices, p.DateTime)).Select(p => (p.DateTime - Start).TotalHours)));
    }

    private static string DescribeCandidates(List<Price> prices, int level)
    {
        var levels = Sawtooth.Levels(prices, EnumPriceBasis.Close);
        return string.Join(" ", Sawtooth.Candidates(prices, levels, EnumPriceBasis.Close, level).Select(c => $"{c.Direction}:{c.Start.Price}>{c.Extreme.Price}"));
    }

    [TestMethod]
    public void Candidates_SeriesHighWithItsPullback()
    {
        Assert.AreEqual("Up:12>11", DescribeCandidates(Closes(10, 12, 11), 1));
    }

    [TestMethod]
    public void Candidates_LastLowOfTheFinalLegWithItsBounce()
    {
        Assert.AreEqual("Up:20>17 Down:17>18", DescribeCandidates(Closes(10, 20, 18, 19, 17, 18), 1));
    }

    [TestMethod]
    public void Candidates_HighOnTheFirstCandle_HasNone()
    {
        Assert.AreEqual("Down:18>19", DescribeCandidates(Closes(20, 18, 19), 1));
    }

    [TestMethod]
    public void Candidates_NoneWhileTheLegIsStillRunning()
    {
        Assert.AreEqual("", DescribeCandidates(Closes(10, 12, 14), 1));
    }

    [TestMethod]
    public void Timeline_CandidateIsConfirmedOnItsBreak()
    {
        var timeline = MarketStructure.Timeline(Closes(10, 12, 11, 11.5, 13), EnumPriceBasis.Close);
        var candidate = timeline.Candidates.Where(x => x.Value.Level == 1 && x.Value.Direction == EnumSwingDirection.Up).ToList();

        Assert.AreEqual("Up:12>11 2-4 Confirmed",
            string.Join("; ", candidate.Select(x => $"{x.Value.Direction}:{x.Value.Start.Price}>{x.Value.Extreme.Price} {x.From}-{x.Until} {x.Ended}")));
        Assert.AreEqual(4, timeline.Swings.Single(x => x.Value.Level == 1).From);
    }

    [TestMethod]
    public void Timeline_PresumedDeathLegSwingsAreRemovedByTheNewHigh()
    {
        var timeline = MarketStructure.Timeline(Closes(10, 20, 17, 18, 15, 16, 14, 21), EnumPriceBasis.Close);

        Assert.AreEqual("Down:17 4-7 Removed; Down:15 6-7 Removed; Up:20 7-",
            string.Join("; ", timeline.Swings.Where(x => x.Value.Level == 1).Select(x => $"{x.Value.Direction}:{x.Value.Start.Price} {x.From}-{x.Until} {x.Ended}".TrimEnd())));
    }

    [TestMethod]
    public void Timeline_PointsAppearOnTheCandleThatMakesThem()
    {
        var timeline = MarketStructure.Timeline(Closes(10, 12, 11, 13, 12, 14, 13), EnumPriceBasis.Close);

        Assert.AreEqual("HH@3>3 HL@4>4 HH@5>5 HL@6>6",
            string.Join(" ", timeline.Points.Where(x => x.Value.Level == 1 && x.Value.Type != null && !x.Continued)
                .Select(x => $"{x.Value.Label}@{(x.Value.Time - Start).TotalHours}>{x.From}")));
    }

    [TestMethod]
    public void Timeline_HigherCloseMovesTheHigherHigh()
    {
        var timeline = MarketStructure.Timeline(Closes(10, 12, 11, 13, 14, 12), EnumPriceBasis.Close);

        Assert.AreEqual("HH@3? 3-4; HH@4? 4-5; HH@4 5-",
            string.Join("; ", timeline.Points.Where(x => x.Value.Level == 1 && x.Value.LegStart == Start.AddHours(2))
                .Select(x => $"{x.Value.Label}@{(x.Value.Time - Start).TotalHours}{(x.Value.Provisional ? "?" : "")} {x.From}-{x.Until}")));
    }

    public static IEnumerable<object[]> Snippets()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "TestData");
        return TermExampleLibrary.Discover(Path.Combine(root, "Terms"))
            .Concat(TermExampleLibrary.Discover(Path.Combine(root, "Structure", "InterimSwings")))
            .Select(x => new object[] { Path.GetRelativePath(root, x.CsvPath) });
    }

    /// <summary>
    /// A level-1 swing is known on the candle that breaks structure: seen live, the same break appears on the same candle.
    /// </summary>
    [DataTestMethod]
    [DynamicData(nameof(Snippets), DynamicDataSourceType.Method)]
    public void RealData_LevelOneBreaksAreSeenLiveOnTheirCandle(string file)
    {
        var prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(Path.Combine(AppContext.BaseDirectory, "TestData", file)).ToList();
        var live = MarketStructure.Replay(prices, EnumPriceBasis.Close, 1).Select(x => (x.Type, x.Time)).ToHashSet();

        var missing = TermAnnotations.Swings(prices, EnumCloseType.Close, 1)
            .Select(x => (Type: x.Direction == EnumSwingDirection.Up ? EnumAnnotationType.BullishBreakOfStructure : EnumAnnotationType.BearishBreakOfStructure, x.BreakOfStructure!.Time))
            .Where(x => !live.Contains(x))
            .ToList();

        Assert.AreEqual(0, missing.Count, $"Not seen live: {string.Join(", ", missing.Select(x => $"{x.Type} {x.Time:u}"))}");
    }

    /// <summary>
    /// A swing that breaks structure live was a candidate on the candle before, with the same start and the same pullback,
    /// at its level or a finer one: a break that also breaks a coarser level's extreme redraws the levels and promotes it.
    /// </summary>
    [DataTestMethod]
    [DynamicData(nameof(Snippets), DynamicDataSourceType.Method)]
    public void RealData_EverySwingWasACandidateFirst(string file)
    {
        var prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(Path.Combine(AppContext.BaseDirectory, "TestData", file)).ToList();
        var timeline = MarketStructure.Timeline(prices, EnumPriceBasis.Close, maxLevel: 8);
        var index = prices.Select((p, i) => (p.DateTime, i)).ToDictionary(x => x.DateTime, x => x.i);

        var unannounced = timeline.Swings
            .Where(x => x.Value.Level <= 3 && x.From == index[x.Value.BreakOfStructure!.Time])
            .Where(x => !timeline.Candidates.Any(c => c.From <= x.From - 1 && (c.Until ?? int.MaxValue) > x.From - 1
                && c.Value.Level >= x.Value.Level && c.Value with { Level = x.Value.Level } == new CandidateSwing(x.Value.Level, x.Value.Direction, x.Value.Start, x.Value.Extreme)))
            .ToList();

        Assert.AreEqual(0, unannounced.Count, $"No candidate before: {string.Join(", ", unannounced.Select(x => $"L{x.Value.Level} {x.Value.Direction} {x.Value.Start.Time:u}"))}");
    }
}
