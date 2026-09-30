using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis.Test.Structure;

[TestClass]
public class SawtoothTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<Price> Closes(params double[] closes) =>
        closes.Select((c, i) => new Price { DateTime = Start.AddHours(i), Open = c, High = c, Low = c, Close = c }).ToList();

    private static string Describe(SawtoothLevel level) =>
        string.Join(" ", level.Pivots.Select(p => $"{p.Kind.ToString()[0]}{p.Index}:{p.Price}"));

    [TestMethod]
    public void LevelZero_IsFirstPriceToHighToCurrent()
    {
        var levels = Sawtooth.Levels(Closes(10, 12, 15, 11, 13), EnumPriceBasis.Close);

        Assert.AreEqual("S0:10 H2:15 C4:13", Describe(levels[0]));
    }

    [TestMethod]
    public void LevelZero_HighIsLastPrice_IsOneLine()
    {
        var levels = Sawtooth.Levels(Closes(10, 12, 14), EnumPriceBasis.Close);

        Assert.AreEqual("S0:10 H2:14", Describe(levels[0]));
    }

    [TestMethod]
    public void LevelZero_HighIsFirstPrice_IsOneLine()
    {
        var levels = Sawtooth.Levels(Closes(15, 12, 13), EnumPriceBasis.Close);

        Assert.AreEqual("H0:15 C2:13", Describe(levels[0]));
    }

    [TestMethod]
    public void LevelZero_TiedHigh_FirstWins()
    {
        var levels = Sawtooth.Levels(Closes(10, 15, 12, 15, 11), EnumPriceBasis.Close);

        Assert.AreEqual("S0:10 H1:15 C4:11", Describe(levels[0]));
    }

    [TestMethod]
    public void DipBeforeFirstHigh_IsTheFirstLowAtLevelOne()
    {
        var levels = Sawtooth.Levels(Closes(10, 8, 9, 12, 11), EnumPriceBasis.Close);

        Assert.AreEqual(2, levels.Count);
        Assert.AreEqual("S0:10 H3:12 C4:11", Describe(levels[0]));
        Assert.AreEqual("S0:10 L1:8 H3:12 C4:11", Describe(levels[1]));
    }

    [TestMethod]
    public void EqualHigh_IsNotANewHigh()
    {
        var levels = Sawtooth.Levels(Closes(10, 12, 11, 12, 14), EnumPriceBasis.Close);

        Assert.AreEqual(2, levels.Count);
        Assert.AreEqual("S0:10 H4:14", Describe(levels[0]));
        Assert.AreEqual("S0:10 H1:12 L2:11 H4:14", Describe(levels[1]));
    }

    [TestMethod]
    public void TiedPullbackLow_FirstWins()
    {
        var levels = Sawtooth.Levels(Closes(10, 12, 11, 11, 14), EnumPriceBasis.Close);

        Assert.AreEqual("S0:10 H1:12 L2:11 H4:14", Describe(levels[1]));
    }

    [TestMethod]
    public void FinalLeg_SplitsIntoLowerLowsThenBounce()
    {
        var levels = Sawtooth.Levels(Closes(10, 20, 15, 17, 12, 14, 13), EnumPriceBasis.Close);

        Assert.AreEqual(3, levels.Count);
        Assert.AreEqual("S0:10 H1:20 C6:13", Describe(levels[0]));
        Assert.AreEqual("S0:10 H1:20 L2:15 H3:17 L4:12 C6:13", Describe(levels[1]));
        Assert.AreEqual("S0:10 H1:20 L2:15 H3:17 L4:12 H5:14 C6:13", Describe(levels[2]));
    }

    [TestMethod]
    public void WickBasis_UsesHighsAndLows()
    {
        var prices = new List<Price>
        {
            new() { DateTime = Start, Open = 10, High = 10.5, Low = 9.5, Close = 10 },
            new() { DateTime = Start.AddHours(1), Open = 10, High = 12.5, Low = 10, Close = 12 },
            new() { DateTime = Start.AddHours(2), Open = 12, High = 13, Low = 10.5, Close = 11 },
        };

        var closes = Sawtooth.Levels(prices, EnumPriceBasis.Close);
        var wicks = Sawtooth.Levels(prices, EnumPriceBasis.Wick);

        Assert.AreEqual("S0:10 H1:12 C2:11", Describe(closes[0]));
        Assert.AreEqual("S0:10 H2:13 C2:11", Describe(wicks[0]));
    }

    private static string DescribeSwings(List<Price> prices, int level)
    {
        var levels = Sawtooth.Levels(prices, EnumPriceBasis.Close);
        return string.Join(" ", Sawtooth.Swings(prices, levels, EnumPriceBasis.Close, level)
            .Select(s => $"{s.Direction}:{s.Start.Price}>{s.Extreme.Price}>{s.BreakOfStructure!.Price}"));
    }

    [TestMethod]
    public void Swings_InUpleg_AreHighPullbackBreak()
    {
        Assert.AreEqual("Up:12>11>13 Up:13>12>15", DescribeSwings(Closes(10, 12, 11, 13, 12, 15, 14), 1));
    }

    [TestMethod]
    public void Swings_InDownleg_AreLowBounceBreak()
    {
        Assert.AreEqual("Down:15>17>14 Down:14>16>13", DescribeSwings(Closes(20, 15, 17, 14, 16, 13, 14), 1));
    }

    [TestMethod]
    public void Swings_LowerLowInsideUpleg_IsNotADownswing()
    {
        Assert.AreEqual("Up:14>11>15 Up:15>9>16", DescribeSwings(Closes(10, 14, 11, 15, 9, 16), 1));
    }

    [TestMethod]
    public void Swings_UnbrokenLastHigh_IsNotASwing()
    {
        Assert.AreEqual("Up:12>11>13", DescribeSwings(Closes(10, 12, 11, 13, 12), 1));
    }

    [TestMethod]
    public void Swings_BreakIsFirstCloseBeyondTheHigh()
    {
        Assert.AreEqual("Up:12>10>12.5", DescribeSwings(Closes(9, 12, 10, 12.5, 11, 14), 1).Split(' ')[0]);
    }

    private static string DescribeBreaks(List<Price> prices, int level)
    {
        var levels = Sawtooth.Levels(prices, EnumPriceBasis.Close);
        var swings = Sawtooth.Swings(prices, levels, EnumPriceBasis.Close, level);
        return string.Join(" ", Sawtooth.MarketStructureBreaks(prices, swings, EnumPriceBasis.Close)
            .Select(m => $"{(m.Type == EnumAnnotationType.BullishMarketStructureBreak ? "Bullish" : "Bearish")}:{m.Reference.Price}>{m.Break.Price}"));
    }

    [TestMethod]
    public void MarketStructureBreak_InsideUpleg_DoesNotEndTheLeg()
    {
        Assert.AreEqual("Bearish:11>10.5", DescribeBreaks(Closes(10, 12, 11, 13, 10.5, 14), 1));
    }

    [TestMethod]
    public void MarketStructureBreak_AfterTheHigh_BreaksTheLastPullbackLow()
    {
        Assert.AreEqual("Bearish:11>10.5", DescribeBreaks(Closes(10, 12, 11, 13, 12, 11.5, 10.5), 1));
    }

    [TestMethod]
    public void MarketStructureBreak_InDownleg_BreaksTheBounceHigh()
    {
        Assert.AreEqual("Bullish:19>19.5", DescribeBreaks(Closes(20, 18, 19, 17, 18, 18.5, 19.5), 1));
    }

    [TestMethod]
    public void MarketStructureBreak_NextBreakOfStructureReplacesTheProtectiveLevel()
    {
        Assert.AreEqual("", DescribeBreaks(Closes(10, 12, 11, 13, 12, 14, 12.5), 1));
    }

    [TestMethod]
    public void ConfirmedIndex_ExtremeAfterItsRun_CounterAtTheNextRun()
    {
        var levels = Sawtooth.Levels(Closes(10, 12, 11, 13, 12), EnumPriceBasis.Close);

        Assert.AreEqual("S0@0 H1@2 L2@3 H3@4 C4@-",
            string.Join(" ", levels[1].Pivots.Select(p => $"{p.Kind.ToString()[0]}{p.Index}@{p.ConfirmedIndex?.ToString() ?? "-"}")));
    }

    [TestMethod]
    public void Swings_BreakOnTheLastCandle_CountsInTheFinalLeg()
    {
        Assert.AreEqual("Down:18>19>17", DescribeSwings(Closes(20, 18, 19, 17), 1));
    }

    private static string DescribeTrends(List<Price> prices, int level)
    {
        var levels = Sawtooth.Levels(prices, EnumPriceBasis.Close);
        var swings = Sawtooth.Swings(prices, levels, EnumPriceBasis.Close, level);
        return string.Join(" ", Sawtooth.Trends(swings, Sawtooth.MarketStructureBreaks(prices, swings, EnumPriceBasis.Close))
            .Select(t => $"{t.Direction}:{t.Confirmed.Price}>{t.End?.Price.ToString() ?? "-"}:{t.Swings}s:{t.MarketStructureBreaks}m"));
    }

    [TestMethod]
    public void Trends_SecondSwingConfirms()
    {
        Assert.AreEqual("Up:15>-:2s:0m", DescribeTrends(Closes(10, 12, 11, 13, 12, 15, 14), 1));
    }

    [TestMethod]
    public void Trends_OneSwing_IsNotATrend()
    {
        Assert.AreEqual("", DescribeTrends(Closes(10, 12, 11, 13, 12), 1));
    }

    [TestMethod]
    public void Trends_EndAtTheFirstSwingTheOtherWay()
    {
        Assert.AreEqual("Up:15>12.5:2s:0m Down:12>-:2s:0m", DescribeTrends(Closes(10, 12, 11, 13, 12, 15, 14, 13, 13.5, 12.5, 13, 12), 1));
    }

    [TestMethod]
    public void Trends_CountMarketStructureBreaksAgainstThem()
    {
        Assert.AreEqual("Up:14>-:2s:1m", DescribeTrends(Closes(10, 12, 11, 13, 10.5, 14), 1));
    }

    [TestMethod]
    public void SwingTree_PullbackDownswingsAreInterimsOfTheUpswing()
    {
        var prices = Closes(10, 20, 17, 18, 15, 16, 14, 21);
        var levels = Sawtooth.Levels(prices, EnumPriceBasis.Close);
        var tree = InterimSwings.Flatten(Sawtooth.SwingTree(prices, levels, EnumPriceBasis.Close, 1, 1));

        Assert.AreEqual("1Up:1-7 2Down:2-4^1 2Down:4-6^1",
            string.Join(" ", tree.Select(x => $"{x.Level}{x.Direction}:{x.Start.Hour}-{x.Break.Hour}{(x.Parent is DateTime p ? $"^{p.Hour}" : "")}")));
    }

    public static IEnumerable<object[]> RealData()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "TestData");
        var files = TermExampleLibrary.Discover(Path.Combine(root, "Terms")).Select(x => x.CsvPath)
            .Append(Path.Combine(root, "PricesExtensionsData", "COINBASE_BTCUSD, 60", "COINBASE_BTCUSD, 60.csv"));
        foreach (var file in files)
            foreach (var basis in Enum.GetValues<EnumPriceBasis>())
                yield return new object[] { Path.GetRelativePath(root, file), basis };
    }

    [DataTestMethod]
    [DynamicData(nameof(RealData), DynamicDataSourceType.Method)]
    public void RealData_LevelsAreWellFormed(string file, EnumPriceBasis basis)
    {
        var prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(Path.Combine(AppContext.BaseDirectory, "TestData", file)).ToList();
        var levels = Sawtooth.Levels(prices, basis, maxLevel: 50);

        for (var l = 0; l < levels.Count; l++)
        {
            var pivots = levels[l].Pivots;
            var turns = pivots.Where(p => p.Kind is EnumPivotKind.High or EnumPivotKind.Low).ToList();

            for (var i = 1; i < pivots.Count; i++)
                Assert.IsTrue(pivots[i].Index > pivots[i - 1].Index || pivots[i].Kind == EnumPivotKind.Current, $"Level {l}: pivots out of order at {i}");

            for (var i = 1; i < turns.Count; i++)
                Assert.AreNotEqual(turns[i - 1].Kind, turns[i].Kind, $"Level {l}: {turns[i].Kind} follows {turns[i - 1].Kind} at {turns[i].Time:u}");

            Assert.IsTrue(pivots.Count(p => p.Kind == EnumPivotKind.Current) <= 1 && (pivots[^1].Kind == EnumPivotKind.Current || pivots.All(p => p.Kind != EnumPivotKind.Current)),
                $"Level {l}: current price must be last");

            if (l > 0)
            {
                var finer = pivots.Select(p => (p.Kind, p.Index)).ToHashSet();
                Assert.IsTrue(levels[l - 1].Pivots.All(p => finer.Contains((p.Kind, p.Index))), $"Level {l} does not contain level {l - 1}");
            }
        }

        var highest = prices.Max(p => basis == EnumPriceBasis.Wick ? p.High : p.Close);
        Assert.AreEqual(highest, levels[0].Pivots.Where(p => p.Kind == EnumPivotKind.High).Max(p => p.Price));
    }
}
