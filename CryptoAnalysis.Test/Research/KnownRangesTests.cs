using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Csv;
using Gradient.CryptoAnalysis.Research;

namespace Gradient.CryptoAnalysis.Test.Research;

/// <summary>
/// The Ranges holding at each candle, on the training's own example (4-hour candles, November 2021): the Range from
/// 55,927 to 59,761, identified at the close of 22 Nov 00:00 and ended by the close of 26 Nov 08:00 below its lower band.
/// </summary>
[TestClass]
public class KnownRangesTests
{
    private static List<Price> Training() =>
        new CsvReaderHelper().ReadData<Price, PriceClassMap>(Path.Combine("TestData", "Terms", "Range", "04-the-training-example.csv")).ToList();

    [TestMethod]
    public void Holding_ARangeHoldsFromTheCloseThatIdentifiesItUntilTheCloseBeyondItsBand()
    {
        var prices = Training();

        var held = KnownRanges.Holding(prices);

        var holding = Enumerable.Range(0, prices.Count).Where(i => held[i].Any(x => x.Level == 1)).ToList();
        CollectionAssert.AreEqual(Enumerable.Range(68, 26).ToList(), holding, "From #68, 22 Nov 00:00, to #93, the candle before the close below the band.");
        Assert.AreEqual(new DateTime(2021, 11, 22, 0, 0, 0), prices[68].DateTime);
        var range = held[80].Single(x => x.Level == 1);
        Assert.AreEqual((55927, 59760, 55160, 60527), ((int)range.Low, (int)range.High, (int)range.BandLow, (int)range.BandHigh));
        Assert.AreEqual((56885, 58802), ((int)range.BuyLowTop(25), (int)range.SellHighBottom(25)));
        Assert.AreEqual((68, EnumSwingDirection.Down), (range.Identified, range.Direction));
        Assert.IsTrue(range.EndedBy(prices[94].Close) && !range.EndedBy(prices[93].Close));
    }

    [TestMethod]
    public void Holding_IsReadFromTheCandlesUpToEachAlone()
    {
        var prices = Training();

        var all = KnownRanges.Read(prices, EnumPriceBasis.Close, 500, 2, Ranges.DefaultMinRetracement, Ranges.DefaultBand);
        var before = KnownRanges.Read(prices.Take(80).ToList(), EnumPriceBasis.Close, 500, 2, Ranges.DefaultMinRetracement, Ranges.DefaultBand);

        for (var i = 0; i < before.Length; i++)
            CollectionAssert.AreEqual(all[i].ToList(), before[i].ToList(), $"#{i}");
    }

    [TestMethod]
    public void Holding_KeptOnDisk_IsReadBackAndOnlyNewCandlesWorkedOut()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"ranges-{Guid.NewGuid():N}");
        var all = Training();
        string Described(IReadOnlyList<KnownRange>[] held) => string.Join(";", held.SelectMany((x, i) => x.Select(r => $"{i}:{r}")));
        var expected = Described(KnownRanges.Read(all, EnumPriceBasis.Close, KnownStructure.Window, KnownStructure.Levels, Ranges.DefaultMinRetracement, Ranges.DefaultBand));
        try
        {
            KnownRanges.CacheDirectory = folder;

            // The first 1000 candles are worked out and kept; a list of the same candles reads them back.
            var worked = Described(KnownRanges.Holding(all.Take(1000).ToList()));
            var file = Directory.GetFiles(folder).Single();
            var written = File.GetLastWriteTimeUtc(file);
            Assert.AreEqual(worked, Described(KnownRanges.Holding(all.Take(1000).ToList())));
            Assert.AreEqual(written, File.GetLastWriteTimeUtc(file), "Nothing new is kept for the same candles.");

            // With candles added, the file's are used and the rest worked out: the whole is as if worked out afresh.
            Assert.AreEqual(expected, Described(KnownRanges.Holding(all.ToList())));
            Assert.AreEqual(1, Directory.GetFiles(folder).Length, "A series that grows keeps the one file.");

            // Other settings are kept in a file of their own.
            KnownRanges.Holding(all.ToList(), minRetracement: 60);
            Assert.AreEqual(2, Directory.GetFiles(folder).Length);
        }
        finally
        {
            KnownRanges.CacheDirectory = null;
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void Holding_ADeeperLeastRetracementLeavesOutAShallowerRange()
    {
        var prices = Training();
        var reading = KnownRanges.Holding(prices)[80].Single(x => x.Level == 1).Retracement;

        Assert.IsTrue(reading is >= 75 and < 95, $"{reading}%");
        Assert.IsFalse(KnownRanges.Holding(prices, minRetracement: 95)[80].Any(x => x.Level == 1));
    }
}
