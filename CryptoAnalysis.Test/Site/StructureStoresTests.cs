using Gradient.CryptoAnalysis.Site;

namespace Gradient.CryptoAnalysis.Test.Site;

/// <summary>
/// What a tour or an analysis reads of the structure is the same looked up in the store held for its series as worked out
/// from every candle (see <see cref="StructureStores"/>).
/// </summary>
[TestClass]
public class StructureStoresTests
{
    private static string Time(Price price) => price.DateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

    // What a reading gives with no store held, and with the store of the prices held.
    private static (T Plain, T Stored) Both<T>(List<Price> prices, Func<T> read)
    {
        Assert.IsNull(StructureStores.Of(prices), "A store is held before the test holds one.");
        var plain = read();
        var store = StructureStore.Read(prices);
        StructureStores.Hold(store);
        try
        {
            Assert.AreSame(store, StructureStores.Of(prices.GetRange(0, prices.Count / 2)), "The first candles of the series find its store.");
            return (plain, read());
        }
        finally
        {
            StructureStores.Drop(store);
        }
    }

    [TestMethod]
    public void OnlyTheFirstCandlesOfASeries_FindItsStore()
    {
        var prices = Btc.From2023().GetRange(0, 400);
        var store = StructureStore.Read(prices.GetRange(0, 300));
        StructureStores.Hold(store);
        try
        {
            Assert.AreSame(store, StructureStores.Of(prices.GetRange(0, 300)));
            Assert.AreSame(store, StructureStores.Of(prices.GetRange(0, 1)));
            Assert.IsNull(StructureStores.Of(prices), "More candles than the store has.");
            Assert.IsNull(StructureStores.Of(prices.GetRange(1, 200)), "Candles from a later one on.");
            Assert.IsNull(StructureStores.Of(prices.Where((_, i) => i % 2 == 0).Take(100).ToList()), "Not the candles one after another.");
            Assert.IsNull(StructureStores.Of([]));
        }
        finally
        {
            StructureStores.Drop(store);
        }

        Assert.IsNull(StructureStores.Of(prices.GetRange(0, 300)));
    }

    [TestMethod]
    public void AnAnalysisOfWhatIsAtACandle_IsWrittenTheSame()
    {
        var prices = Btc.From2023().GetRange(0, 2000);
        var (plain, stored) = Both(prices, () =>
        {
            var written = new List<string>();
            var rng = new Random(11);
            for (var i = 0; i < 12; i++)
            {
                var cursor = rng.Next(200, prices.Count);
                var candle = i % 3 == 0 ? cursor : rng.Next(Math.Max(0, cursor - 150), cursor + 1);
                var inPlace = i % 2 == 0;
                var things = Explain.At(prices, Time(prices[candle]), Time(prices[cursor])).Where(x => x.Kind is "Swing" or "Trend" or "Point").ToList();
                var def = Explain.Tour(things, "test", prices, Time(prices[candle]), Time(prices[cursor]), out var missing, inPlace);
                var tour = def == null ? null : Tours.Compile(def, prices);
                written.Add($"{candle} seen at {cursor}: {string.Join(",", things.Select(x => x.Id))} missing {string.Join(",", missing)}\n{def?.ToJsonString()}\n{tour?.Expanded?.ToJsonString()}\n{string.Join("|", tour?.Errors ?? [])}");
                foreach (var level in new[] { 1, 3, 6 })
                    written.Add($"trend shown L{level}: {Explain.TrendShown(prices, Time(prices[candle]), level, Time(prices[cursor]), inPlace)?.ToJsonString()}");
            }

            return string.Join("\n\n", written);
        });

        Assert.AreEqual(plain, stored);
        Assert.IsTrue(plain.Split("\"explain\"").Length > 20 && plain.Contains("\"cues\""), "Too little was written to compare.");
    }

    [TestMethod]
    public void EveryTrendOfADataset_IsListedTheSame()
    {
        var prices = Btc.From2023().GetRange(0, 1200);

        var (plain, stored) = Both(prices, () => Explain.AllTrends(prices, 8).ToJsonString());

        Assert.AreEqual(plain, stored);
        Assert.IsTrue(plain.Split("\"level\"").Length > 50);
    }

    [TestMethod]
    public void AnEventATourWaitsFor_IsFoundAtTheSameCandle()
    {
        var prices = Btc.From2023().GetRange(0, 900);
        var types = new[]
        {
            EnumAnnotationType.BullishBreakOfStructure, EnumAnnotationType.BearishBreakOfStructure, EnumAnnotationType.BullishMarketStructureBreak,
            EnumAnnotationType.BearishMarketStructureBreak, EnumAnnotationType.Uptrend, EnumAnnotationType.Downtrend, EnumAnnotationType.HigherHigh,
            EnumAnnotationType.HigherLow, EnumAnnotationType.LowerHigh, EnumAnnotationType.LowerLow,
        };

        var (plain, stored) = Both(prices, () => string.Join("\n", from type in types from level in new[] { 1, 2, 4 } from start in new[] { 0, 240 } from nth in new[] { 1, 3 }
            select $"{type} L{level} from {start} nth {nth}: {Tours.FindEvent(prices, start, type, level, nth)}"));

        Assert.AreEqual(plain, stored);
        Assert.IsTrue(plain.Split('\n').Count(x => !x.EndsWith(": ")) > 60, "Too few of the events are found at all.");
    }
}
