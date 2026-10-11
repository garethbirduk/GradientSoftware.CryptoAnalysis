using Gradient.CryptoAnalysis.Site;

namespace Gradient.CryptoAnalysis.Test.Site;

/// <summary>
/// What is at a candle is the same looked up in the structure store as worked out from every candle.
/// </summary>
[TestClass]
public class ExplainFromStoreTests
{
    private static string Time(Price price) => price.DateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

    private static string Describe(IEnumerable<Explain.Thing> things) => string.Join(", ", things.Select(x => x.Name));

    private static void AssertSame(List<Price> prices, StructureStore store, int candle, int? cursor)
    {
        var seen = cursor is int c ? Time(prices[c]) : null;
        Assert.AreEqual(Describe(Explain.At(prices, Time(prices[candle]), seen)), Describe(Explain.At(prices, Time(prices[candle]), seen, store)),
            $"The candle {candle} as seen at {cursor?.ToString() ?? "the end"}.");
    }

    [TestMethod]
    public void EveryCandleOfAStretch_AsSeenAtItsEnd()
    {
        var prices = Btc.From2023().GetRange(0, 1200);
        var store = StructureStore.Read(prices);

        for (var candle = 0; candle < prices.Count; candle++)
            AssertSame(prices, store, candle, null);
    }

    [TestMethod]
    public void CandlesAsSeenAtCursorsBeforeTheEnd_TheCursorsOwnCandleAmongThem()
    {
        var prices = Btc.From2023().GetRange(0, 3000);
        var store = StructureStore.Read(prices);
        var random = new Random(7);

        var labelled = 0;
        for (var i = 0; i < 400; i++)
        {
            var cursor = random.Next(1, prices.Count);
            // The cursor's own candle, the few before it where the leg still runs, and any candle before.
            var candle = (i % 4) switch { 0 => cursor, 1 => Math.Max(0, cursor - random.Next(1, 12)), _ => random.Next(0, cursor + 1) };
            AssertSame(prices, store, candle, cursor);
            if (Explain.At(prices, Time(prices[candle]), Time(prices[cursor]), store).Any(x => x.Kind == "Point"))
                labelled++;
        }

        Assert.IsTrue(labelled > 50, $"Only {labelled} of the candles tried have a labelled high or low.");
    }

    [TestMethod]
    public void EachCandleOfARunToANewHigh_AsSeenOnIt()
    {
        // The highest high so far is on the cursor's candle through much of January 2023.
        var prices = Btc.From2023().GetRange(0, 700);
        var store = StructureStore.Read(prices);

        for (var cursor = 1; cursor < prices.Count; cursor++)
        {
            AssertSame(prices, store, cursor, cursor);
            AssertSame(prices, store, cursor - 1, cursor);
        }
    }
}