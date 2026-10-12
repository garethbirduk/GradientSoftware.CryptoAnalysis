using Gradient.CryptoAnalysis.Research;
using Flag = Gradient.CryptoAnalysis.Research.KnownStructure.EnumFlag;

namespace Gradient.CryptoAnalysis.Test.Research;

[TestClass]
public class KnownStructureTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<Price> Closes(params double[] closes) =>
        closes.Select((c, i) => new Price { DateTime = Start.AddHours(i), Open = c, High = c, Low = c, Close = c }).ToList();

    private static string At(int[] masks, int level, Flag flag) =>
        string.Join(" ", masks.Select((m, i) => (m & (1 << KnownStructure.Bit(level, flag))) != 0 ? i : -1).Where(i => i >= 0));

    [TestMethod]
    public void Masks_AreTheReplaysEventsOnTheCandlesTheyBecomeKnown()
    {
        var masks = KnownStructure.Masks(Closes(10, 12, 11, 13, 12, 15), EnumPriceBasis.Close, 100, 2);

        Assert.AreEqual("3 5", At(masks, 1, Flag.BreakUp));
        Assert.AreEqual("5", At(masks, 1, Flag.Uptrend));
        Assert.AreEqual("", At(masks, 1, Flag.BreakDown));
    }

    [TestMethod]
    public void Masks_PresumedDeathLegDowntrendHoldsUntilTheNewHigh()
    {
        var masks = KnownStructure.Masks(Closes(10, 20, 17, 18, 15, 16, 14, 21), EnumPriceBasis.Close, 100, 1);

        Assert.AreEqual("4 6", At(masks, 1, Flag.BreakDown));
        Assert.AreEqual("6", At(masks, 1, Flag.Downtrend));
        Assert.AreEqual("7", At(masks, 1, Flag.BreakUp));
    }

    [TestMethod]
    public void Masks_SeeOnlyTheWindow()
    {
        Assert.AreEqual("", At(KnownStructure.Masks(Closes(10, 12, 11, 13, 12, 15), EnumPriceBasis.Close, 2, 1), 1, Flag.BreakUp));
    }

    [TestMethod]
    public void Breaks_SayWhatEachWasReadFrom()
    {
        // Upswings from the highs of 12 and 13; the close of 10.5 is below the first's Swing Low of 11, inside the second.
        var breaks = KnownStructure.Read(Closes(10, 12, 11, 13, 10.5, 12.5, 14), EnumPriceBasis.Close, 100, 1).Breaks;

        Assert.AreEqual("3:BreakOfStructure Up from 1, 1 in a row from 1; 4:MarketStructureBreak Down from 2; 6:BreakOfStructure Up from 3, 2 in a row from 1, Weak",
            string.Join("; ", breaks.SelectMany((x, i) => x.Select(b => b.Kind == EnumKnownBreak.MarketStructureBreak
                ? $"{i}:{b.Kind} {b.Direction} from {b.From}"
                : $"{i}:{b.Kind} {b.Direction} from {b.From}, {b.Run} in a row from {b.Begins}{(b.Weak ? ", Weak" : "")}"))));
    }

    [TestMethod]
    public void Breaks_AreTheFlagsBreaksAndCountWhatTheTrendFlagsHold()
    {
        var prices = Synthetic.Walk(5, Start, 1500);

        var (masks, breaks) = KnownStructure.Read(prices, EnumPriceBasis.Close, 200, KnownStructure.Levels);

        bool Flagged(int i, int level, Flag flag) => (masks[i] & (1 << KnownStructure.Bit(level, flag))) != 0;
        for (var i = 0; i < prices.Count; i++)
        {
            for (var level = 1; level <= KnownStructure.Levels; level++)
            {
                bool Made(EnumKnownBreak kind, EnumSwingDirection direction) => breaks[i].Any(x => x.Level == level && x.Kind == kind && x.Direction == direction);
                Assert.AreEqual(Flagged(i, level, Flag.BreakUp), Made(EnumKnownBreak.BreakOfStructure, EnumSwingDirection.Up), $"#{i} level {level}");
                Assert.AreEqual(Flagged(i, level, Flag.BreakDown), Made(EnumKnownBreak.BreakOfStructure, EnumSwingDirection.Down), $"#{i} level {level}");
                Assert.AreEqual(Flagged(i, level, Flag.BullishMarketStructureBreak), Made(EnumKnownBreak.MarketStructureBreak, EnumSwingDirection.Up), $"#{i} level {level}");
                Assert.AreEqual(Flagged(i, level, Flag.BearishMarketStructureBreak), Made(EnumKnownBreak.MarketStructureBreak, EnumSwingDirection.Down), $"#{i} level {level}");
            }

            // A break of structure that makes two or more in a row is a Trend holding, and one that makes one is none.
            foreach (var b in breaks[i].Where(x => x.Kind == EnumKnownBreak.BreakOfStructure))
            {
                Assert.AreEqual(b.Run >= 2, Flagged(i, b.Level, b.Direction == EnumSwingDirection.Up ? Flag.Uptrend : Flag.Downtrend), $"#{i} level {b.Level}");
                Assert.IsTrue(b.Begins <= b.From && b.From < i && (b.Run > 1 || b.Begins == b.From), $"#{i} level {b.Level}");
            }
        }

        Assert.IsTrue(breaks.Sum(x => x.Count(b => b.Run >= 3)) > 20, "A random walk has Trends of three Swings and more.");
    }

    private static string Described(IReadOnlyList<KnownBreak>[] breaks) =>
        string.Join(";", breaks.SelectMany((x, i) => x.Select(b => $"{i}:{b}")));

    [TestMethod]
    public void Breaks_KeptOnDisk_AreReadBackAndOnlyNewCandlesWorkedOut()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"known-{Guid.NewGuid():N}");
        var all = Synthetic.Walk(7, Start, 1400);
        var expected = Described(KnownStructure.Read(all, EnumPriceBasis.Close, KnownStructure.Window, KnownStructure.Levels).Breaks);
        try
        {
            KnownStructure.CacheDirectory = folder;

            // The first 1000 candles are worked out and kept; a list of the same candles reads them back.
            var first = all.Take(1000).ToList();
            var worked = Described(KnownStructure.Breaks(first));
            var file = Directory.GetFiles(folder).Single();
            var written = File.GetLastWriteTimeUtc(file);
            Assert.AreEqual(worked, Described(KnownStructure.Breaks(all.Take(1000).ToList())));
            Assert.AreEqual(written, File.GetLastWriteTimeUtc(file), "Nothing new is kept for the same candles.");

            // With candles added, the file's are used and the rest worked out: the whole is as if worked out afresh.
            Assert.AreEqual(expected, Described(KnownStructure.Breaks(all.ToList())));
            Assert.AreEqual(1, Directory.GetFiles(folder).Length, "A series that grows keeps the one file.");

            // A candle changed among those kept: what is kept is not used.
            var changed = all.Select(p => new Price { DateTime = p.DateTime, Open = p.Open, High = p.High, Low = p.Low, Close = p.Close }).ToList();
            changed[700].Close = changed[700].High = changed[700].Close * 1.2;
            Assert.AreEqual(Described(KnownStructure.Read(changed, EnumPriceBasis.Close, KnownStructure.Window, KnownStructure.Levels).Breaks), Described(KnownStructure.Breaks(changed)));
        }
        finally
        {
            KnownStructure.CacheDirectory = null;
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void Masks_OfACandleDoNotChangeWithTheCandlesAfterIt()
    {
        var prices = Synthetic.Walk(3, Start, 900);

        var all = KnownStructure.Masks(prices, EnumPriceBasis.Close, 200, KnownStructure.Levels);
        var before = KnownStructure.Masks(prices.Take(600).ToList(), EnumPriceBasis.Close, 200, KnownStructure.Levels);

        CollectionAssert.AreEqual(before, all.Take(600).ToArray());
        Assert.IsTrue(all.Count(x => x != 0) > 300, "A random walk shows structure on most candles.");
    }
}
