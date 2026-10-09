namespace Gradient.CryptoAnalysis.Strategies;

/// <summary>
/// One total of a baseline: the strategy's own, and how the random runs spread around it. Low and High are the 5th and 95th
/// percentiles of the runs, so nine runs in ten fall between them. Percentile is the share of runs the strategy beat, as a
/// percentage, a tie counting as half; null when the strategy has no value for it (a profit factor with no losses).
/// </summary>
public sealed record BaselineMeasure(string Name, double? Strategy, double Mean, double Low, double Median, double High, double? Percentile);

/// <summary>
/// A point of a baseline's curve: the profit, as the sum of the ProfitPercent of the trades closed by the candle at Time, of
/// the strategy and of the random runs, Low, Median and High being their 5th, 50th and 95th percentiles there.
/// </summary>
public sealed record BaselinePoint(DateTime Time, double Strategy, double Low, double Median, double High);

/// <summary>
/// A strategy set against random entries over a dataset (see <see cref="StrategyBaseline"/>): what was run, how often the
/// random runs tried to enter, the trades they made on average, each total, and the profit over time.
/// </summary>
public sealed record BaselineRun(Strategy Strategy, string Dataset, int Candles, int Runs, int Seed, int Signals, double Chance,
    int Trades, double AverageTrades, DateTime Run, List<BaselineMeasure> Measures, List<BaselinePoint> Curve);

/// <summary>
/// The random-entry baseline of a strategy: the same strategy run many times with its entry condition swapped for random
/// candles, to tell what its entry adds from what its exits and the market's direction make on their own. Each run tries to
/// enter at each candle by the same chance, the share of candles the strategy's entry condition is met at, so the runs try as
/// often as the strategy does; the direction, the Take Profit and Stop Loss and OnePositionAtATime are the strategy's own.
/// </summary>
public static class StrategyBaseline
{
    public const int DefaultRuns = 1000;
    public const int MaxRuns = 10000;

    public const string WinRate = "Win rate";
    public const string ProfitFactor = "Profit factor";
    public const string AverageProfitPercent = "Average %";
    public const string TotalProfitPercent = "Total %";

    /// <summary>
    /// Runs the strategy and the given number of random-entry runs over the prices and sets each total of the strategy
    /// against theirs. The same seed gives the same runs.
    /// </summary>
    public static BaselineRun Run(IReadOnlyList<Price> prices, Strategy strategy, string dataset = "", int runs = DefaultRuns, int seed = 1)
    {
        if (runs < 1 || runs > MaxRuns)
            throw new ArgumentOutOfRangeException(nameof(runs), $"The runs must be from 1 to {MaxRuns}.");
        var actual = StrategyBacktest.Run(prices, strategy, dataset);
        var signals = StrategyBacktest.Signals(prices, strategy).Count;
        if (signals == 0)
            throw new ArgumentException("The strategy's entry condition is not met anywhere in the prices, so there is no rate to match.", nameof(strategy));

        var chance = (double)signals / prices.Count;
        var points = CurvePoints(prices.Count);
        var random = new BacktestSummary[runs];
        var curves = new double[runs][];
        Parallel.For(0, runs, r =>
        {
            var trades = StrategyBacktest.Trades(prices, strategy, Entries(prices.Count, chance, seed + r));
            random[r] = StrategyBacktest.Summarise(trades);
            curves[r] = Cumulative(trades, points);
        });
        var own = Cumulative(actual.Trades, points);
        var curve = points.Select((at, k) =>
        {
            var sorted = curves.Select(c => c[k]).Order().ToList();
            return new BaselinePoint(prices[at].DateTime, own[k], Quantile(sorted, 5), Quantile(sorted, 50), Quantile(sorted, 95));
        }).ToList();

        var measures = new List<BaselineMeasure>
        {
            Measure(WinRate, actual.Summary.Trades > 0 ? actual.Summary.WinRate : null, random.Where(x => x.Trades > 0).Select(x => x.WinRate)),
            Measure(ProfitFactor, actual.Summary.ProfitFactor, random.Select(x => x.ProfitFactor).OfType<double>()),
            Measure(AverageProfitPercent, Average(actual.Summary), random.Select(Average).OfType<double>()),
            Measure(TotalProfitPercent, actual.Summary.TotalProfitPercent, random.Select(x => x.TotalProfitPercent)),
        };
        return new BaselineRun(strategy, dataset, prices.Count, runs, seed, signals, chance, actual.Summary.Trades,
            random.Average(x => x.Trades), DateTime.UtcNow, measures, curve);
    }

    /// <summary>
    /// The candles a curve is measured at: evenly spread from the first to the last, no more than the given number of them.
    /// </summary>
    public static List<int> CurvePoints(int candles, int most = 300)
    {
        if (candles <= 0)
            return [];
        var count = Math.Min(candles, most);
        return count == 1 ? [candles - 1] : Enumerable.Range(0, count).Select(k => (int)Math.Round((double)k * (candles - 1) / (count - 1))).Distinct().ToList();
    }

    /// <summary>
    /// The profit at each of the candles, as the sum of the ProfitPercent of the trades closed by it; a trade still open at
    /// the end is not counted, as in the totals.
    /// </summary>
    public static double[] Cumulative(IReadOnlyList<StrategyTrade> trades, IReadOnlyList<int> points)
    {
        var closed = trades.Where(x => x.Outcome != EnumTradeOutcome.Open).OrderBy(x => x.ExitIndex).ToList();
        var sums = new double[points.Count];
        var (next, total) = (0, 0.0);
        for (var k = 0; k < points.Count; k++)
        {
            for (; next < closed.Count && closed[next].ExitIndex <= points[k]; next++)
                total += closed[next].ProfitPercent;
            sums[k] = total;
        }

        return sums;
    }

    /// <summary>
    /// The candles a random run tries to enter at: each of the candles by the chance, from the seed.
    /// </summary>
    public static IEnumerable<(int Index, IReadOnlyList<int> Met)> Entries(int candles, double chance, int seed)
    {
        var random = new Random(seed);
        for (var i = 0; i < candles; i++)
        {
            if (random.NextDouble() < chance)
                yield return (i, []);
        }
    }

    /// <summary>
    /// The value at a percentile (0 to 100) of sorted values, between the two nearest when it falls between them.
    /// </summary>
    public static double Quantile(IReadOnlyList<double> sorted, double percentile)
    {
        if (sorted.Count == 0)
            return 0;
        var at = (sorted.Count - 1) * percentile / 100;
        var below = (int)Math.Floor(at);
        var above = Math.Min(below + 1, sorted.Count - 1);
        return sorted[below] + (sorted[above] - sorted[below]) * (at - below);
    }

    private static double? Average(BacktestSummary summary) => summary.Trades > 0 ? summary.TotalProfitPercent / summary.Trades : null;

    private static BaselineMeasure Measure(string name, double? strategy, IEnumerable<double> runs)
    {
        var sorted = runs.Order().ToList();
        double? percentile = strategy is double value && sorted.Count > 0
            ? 100 * (sorted.Count(x => x < value) + 0.5 * sorted.Count(x => x == value)) / sorted.Count
            : null;
        return new BaselineMeasure(name, strategy, sorted.Count > 0 ? sorted.Average() : 0, Quantile(sorted, 5), Quantile(sorted, 50),
            Quantile(sorted, 95), percentile);
    }
}
