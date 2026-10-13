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

        var all = KnownRanges.Holding(prices);
        var before = KnownRanges.Holding(prices.Take(80).ToList());

        for (var i = 0; i < before.Length; i++)
            CollectionAssert.AreEqual(all[i].ToList(), before[i].ToList(), $"#{i}");
    }

    [TestMethod]
    public void Holding_IsTheRangeOfEveryCandleToEachReadAgain()
    {
        var prices = Btc.From2023().Take(2000).ToList();

        var held = KnownRanges.Holding(prices);

        Assert.IsTrue(held.Count(x => x.Count > 0) > 100, "Ranges hold over many of the candles.");
        foreach (var t in Enumerable.Range(0, prices.Count).Where(t => t % 53 == 0 || t > prices.Count - 100))
        {
            // Each level's Range that holds at the candle, the last identified, as the Sawtooth and Ranges read the candles to it.
            var read = prices.GetRange(0, t + 1);
            var sawtooth = Sawtooth.Levels(read, EnumPriceBasis.Close, 64);
            var plain = new List<string>();
            for (var level = 1; level + 1 < sawtooth.Count; level++)
            {
                List<SwingOutline> Swings(int l) => Sawtooth.Swings(read, sawtooth, EnumPriceBasis.Close, l).OrderBy(x => x.Start.Time).ToList();
                var swings = Swings(level);
                var breaks = Sawtooth.MarketStructureBreaks(read, swings, EnumPriceBasis.Close);
                var trends = Sawtooth.Trends(swings, breaks);
                if (trends.Count == 0)
                    continue;
                var retracements = Sawtooth.Retracements(read, swings, Sawtooth.Candidates(read, sawtooth, EnumPriceBasis.Close, level), EnumPriceBasis.Close);
                var finer = Sawtooth.MarketStructureBreaks(read, Swings(level + 1), EnumPriceBasis.Close);
                if (Ranges.Find(read, EnumPriceBasis.Close, retracements, swings, trends, finer, Ranges.DefaultMinRetracement, Ranges.DefaultBand)
                    .Where(x => x.End == null).MaxBy(x => x.Identified.Time) is { } range)
                    plain.Add($"{level} {range.Direction} {range.Low.Price} {range.High.Price} {range.Retracement}% at {read.FindIndex(x => x.DateTime == range.Identified.Time)}");
            }

            Assert.AreEqual(string.Join("; ", plain),
                string.Join("; ", held[t].OrderBy(x => x.Level).Select(x => $"{x.Level} {x.Direction} {x.Low} {x.High} {x.Retracement}% at {x.Identified}")), $"#{t}");
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
