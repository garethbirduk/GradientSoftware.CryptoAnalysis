namespace Gradient.CryptoAnalysis.Research;

/// <summary>
/// Markets made up from a seed, where the truth is known, to check that the ways strategies and conditions are judged tell an
/// edge from luck: a random walk has nothing to find, and a planted edge has one thing of a known size.
/// </summary>
public static class Synthetic
{
    /// <summary>
    /// The typical move of an hour of BTC, as the standard deviation of its close-to-close return: about 0.6%.
    /// </summary>
    public const double HourlyVolatility = 0.006;

    /// <summary>
    /// Hourly candles from start, one after another, each opening at the close before it with a return drawn from a normal
    /// distribution of the given volatility, plus the drift given for it from the candles before it (none by default), and a
    /// wick either side. The same seed gives the same candles.
    /// </summary>
    public static List<Price> Walk(int seed, DateTime start, int candles, double volatility = HourlyVolatility, Func<IReadOnlyList<Price>, double>? drift = null)
    {
        var random = new Random(seed);
        var prices = new List<Price>(candles);
        var close = 10000.0;
        for (var i = 0; i < candles; i++)
        {
            var open = close;
            close = open * Math.Exp(volatility * Normal(random) + (drift?.Invoke(prices) ?? 0));
            var high = Math.Max(open, close) * Math.Exp(Math.Abs(volatility / 2 * Normal(random)));
            var low = Math.Min(open, close) * Math.Exp(-Math.Abs(volatility / 2 * Normal(random)));
            prices.Add(new Price { DateTime = start.AddHours(i), Open = open, High = high, Low = low, Close = close });
        }

        return prices;
    }

    /// <summary>
    /// A random walk in which the candle after every run of at least three green candles drifts up by the given share of
    /// the volatility (a quarter by default, which makes it green about 60% of the time rather than 50%), and nothing else
    /// does: the one edge to be found.
    /// </summary>
    public static List<Price> PlantedEdge(int seed, DateTime start, int candles, double strength = 0.25, double volatility = HourlyVolatility) =>
        Walk(seed, start, candles, volatility, prices => prices.Count >= 3 && Enumerable.Range(prices.Count - 3, 3).All(i => prices[i].Close > prices[i].Open)
            ? strength * volatility
            : 0);

    // A draw from the standard normal distribution, by the Box-Muller transform.
    private static double Normal(Random random) =>
        Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());
}
