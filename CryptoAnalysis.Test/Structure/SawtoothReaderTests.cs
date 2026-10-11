using Gradient.CryptoAnalysis.Research;

namespace Gradient.CryptoAnalysis.Test.Structure;

/// <summary>
/// The reader has, after each candle, what the sawtooth makes of the candles so far when it reads them all again.
/// </summary>
[TestClass]
public class SawtoothReaderTests
{
    // More levels than any series here has, so every level is read.
    private const int Depth = 60;

    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static string Describe(IReadOnlyList<SawtoothLevel> levels) =>
        string.Join("\n", levels.Select(l => $"{l.Level}: " + string.Join(" ", l.Pivots.Select(p => $"{p.Kind.ToString()[0]}{p.Index}:{p.Price}/{p.ConfirmedIndex}"))));

    private static string Describe(IEnumerable<SwingOutline> swings) =>
        string.Join("\n", swings.Select(x => $"L{x.Level} {x.Direction} {x.Start.Time:yyMMddHH}:{x.Start.Price} {x.Extreme.Time:yyMMddHH}:{x.Extreme.Price} {x.BreakOfStructure!.Time:yyMMddHH}:{x.BreakOfStructure.Price} {x.End == x.BreakOfStructure}"));

    private static string Json<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value);

    /// <summary>
    /// Reads the candles one at a time and, at each candle check says to, holds the reader to the sawtooth of the candles
    /// to there.
    /// </summary>
    private static void AssertSame(List<Price> prices, EnumPriceBasis basis, Func<int, bool> check, string name)
    {
        var reader = new SawtoothReader(basis);
        for (var i = 0; i < prices.Count; i++)
        {
            reader.Add(prices[i]);
            if (!check(i))
                continue;

            var so = prices.GetRange(0, i + 1);
            var expected = Sawtooth.Levels(so, basis, Depth);
            Assert.AreEqual(Describe(expected), Describe(reader.Levels(Depth)), $"{name}: the levels at candle {i}.");
            for (var level = 0; level < expected.Count; level++)
                Assert.AreEqual(Json(Sawtooth.Points(so, expected[level], basis)), Json(reader.Points(level)), $"{name}: the points of level {level} at candle {i}.");
            Assert.AreEqual(0, reader.Points(expected.Count).Count, $"{name}: points of a level there is not, at candle {i}.");

            // One level past the last, which has nothing.
            for (var level = 1; level <= expected.Count; level++)
            {
                var swings = Sawtooth.Swings(so, expected, basis, level);
                var candidates = Sawtooth.Candidates(so, expected, basis, level);
                Assert.AreEqual(Describe(swings), Describe(reader.Swings(level)), $"{name}: the swings of level {level} at candle {i}.");
                Assert.AreEqual(Json(candidates), Json(reader.Candidates(level)), $"{name}: the candidates of level {level} at candle {i}.");
                Assert.AreEqual(Json(Sawtooth.Retracements(so, swings, candidates, basis)), Json(reader.Retracements(level)), $"{name}: the retracements of level {level} at candle {i}.");
            }
        }
    }

    [TestMethod]
    [DataRow(EnumPriceBasis.Close)]
    [DataRow(EnumPriceBasis.Wick)]
    public void ShortSeriesOfFewPrices_WithTheirTies_AreTheSawtoothAtEveryCandle(EnumPriceBasis basis)
    {
        // Few prices to choose from, so highs and lows are often equalled: ties, first candles that are the high, flat runs.
        for (var seed = 0; seed < 400; seed++)
        {
            var random = new Random(seed);
            var close = (double)random.Next(1, 7);
            var prices = new List<Price>();
            for (var i = 0; i < 40; i++)
            {
                var open = close;
                close = Math.Clamp(close + random.Next(-2, 3), 1, 8);
                prices.Add(new Price { DateTime = Start.AddHours(i), Open = open, Close = close, High = Math.Max(open, close) + random.Next(0, 3), Low = Math.Min(open, close) - random.Next(0, 3) });
            }

            AssertSame(prices, basis, _ => true, $"Seed {seed}");
        }
    }

    [TestMethod]
    [DataRow(EnumPriceBasis.Close)]
    [DataRow(EnumPriceBasis.Wick)]
    public void RandomWalks_AreTheSawtoothAtEveryCandle(EnumPriceBasis basis)
    {
        for (var seed = 1; seed <= 4; seed++)
            AssertSame(Synthetic.Walk(seed, Start, 1500), basis, _ => true, $"Walk {seed}");
    }

    [TestMethod]
    public void Btc_Through2023_IsTheSawtoothAtEveryCandle()
    {
        AssertSame(Btc.From2023().GetRange(0, 2500), EnumPriceBasis.Close, _ => true, "BTC from 2023");
    }

    [TestMethod]
    public void Btc_From2020_IsTheSawtooth_AcrossTheHighOf2021BeingPassed()
    {
        var all = Btc.From2020();
        var high = Enumerable.Range(0, 30000).MaxBy(i => all[i].Close);
        var crossing = Enumerable.Range(30000, all.Count - 30000).First(i => all[i].Close > all[high].Close);

        // Every 2,000th candle, each candle either side of the close above the old high, and the last.
        AssertSame(all, EnumPriceBasis.Close, i => i % 2000 == 0 || Math.Abs(i - crossing) <= 10 || i == all.Count - 1, "BTC from 2020");
    }
}
