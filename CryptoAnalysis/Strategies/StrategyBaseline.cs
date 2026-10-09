namespace Gradient.CryptoAnalysis.Strategies;

/// <summary>
/// One total of a baseline: the strategy's own, and how the random runs spread around it. Low and High are the 5th and 95th
/// percentiles of the runs, so nine runs in ten fall between them. Percentile is the share of runs the strategy beat, as a
/// percentage, a tie counting as half; null when the strategy has no value for it (a profit factor with no losses).
/// </summary>
public sealed record BaselineMeasure(string Name, double? Strategy, double Mean, double Low, double Median, double High, double? Percentile);

/// <summary>
/// A point of a baseline's curve: the profit, as the sum of the ProfitPercent of the trades closed by the candle at Time, of
/// the strategy and of the random runs, Low, Median and High being their 5th, 50th and 95th percentiles there. Gross is the
/// strategy's before fees and slippage; the others are after them.
/// </summary>
public sealed record BaselinePoint(DateTime Time, double Strategy, double Low, double Median, double High, double Gross);

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
    /// The totals a baseline sets the strategy against the random runs by, in order, each read from a run's summary; null
    /// where a run has none (a win rate with no trades closed, a profit factor with no losses).
    /// </summary>
    public static readonly IReadOnlyList<(string Name, Func<BacktestSummary, double?> Of)> Totals =
    [
        (WinRate, x => x.Trades > 0 ? x.WinRate : null),
        (ProfitFactor, x => x.ProfitFactor),
        (AverageProfitPercent, x => x.Trades > 0 ? x.TotalProfitPercent / x.Trades : null),
        (TotalProfitPercent, x => x.TotalProfitPercent),
    ];

    /// <summary>
    /// Runs the strategy and the given number of random-entry runs over the prices and sets each total of the strategy
    /// against theirs. The same seed gives the same runs. With a window, the strategy and the random runs enter only in it,
    /// as <see cref="StrategyBacktest.Run"/> does.
    /// </summary>
    public static BaselineRun Run(IReadOnlyList<Price> prices, Strategy strategy, string dataset = "", int runs = DefaultRuns, int seed = 1,
        (int First, int End)? window = null) =>
        Measured(prices, strategy, dataset, runs, seed, window).Baseline;

    /// <summary>
    /// The baseline, with the strategy's own totals and those of each random run, in the order of their seeds.
    /// </summary>
    internal static (BaselineRun Baseline, BacktestSummary Actual, BacktestSummary[] Random) Measured(IReadOnlyList<Price> prices, Strategy strategy,
        string dataset, int runs, int seed, (int First, int End)? window = null)
    {
        if (runs < 1 || runs > MaxRuns)
            throw new ArgumentOutOfRangeException(nameof(runs), $"The runs must be from 1 to {MaxRuns}.");
        var (first, end) = window ?? (0, prices.Count);
        var actual = StrategyBacktest.Run(prices, strategy, dataset, (first, end));
        var signals = StrategyBacktest.Signals(prices, strategy).Count(x => x.Index >= first && x.Index < end);
        if (signals == 0)
            throw new ArgumentException("The strategy's entry condition is not met anywhere in the prices, so there is no rate to match.", nameof(strategy));

        var chance = (double)signals / (end - first);
        var points = CurvePoints(end - first).Select(x => x + first).ToList();
        var random = new BacktestSummary[runs];
        var curves = new double[runs][];
        Parallel.For(0, runs, r =>
        {
            var trades = StrategyBacktest.Trades(prices, strategy, Entries(first, end, chance, seed + r));
            random[r] = StrategyBacktest.Summarise(trades);
            curves[r] = Cumulative(trades, points);
        });
        var own = Cumulative(actual.Trades, points);
        var gross = Cumulative(actual.Trades, points, x => 100 * (x.Profit + x.Fees + x.Slippage) / x.EntryPrice);
        var curve = points.Select((at, k) =>
        {
            var sorted = curves.Select(c => c[k]).Order().ToList();
            return new BaselinePoint(prices[at].DateTime, own[k], Quantile(sorted, 5), Quantile(sorted, 50), Quantile(sorted, 95), gross[k]);
        }).ToList();

        var measures = Totals.Select(t => Measure(t.Name, t.Of(actual.Summary), random.Select(t.Of).OfType<double>())).ToList();
        return (new BaselineRun(strategy, dataset, end - first, runs, seed, signals, chance, actual.Summary.Trades,
            random.Average(x => x.Trades), DateTime.UtcNow, measures, curve), actual.Summary, random);
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
    /// The profit at each of the candles, as the sum of each trade's percentage closed by it: its ProfitPercent, or what
    /// percent gives. A trade that closes after the last candle, as one entered near the end of a window can, is counted at
    /// the last, so the curve ends at the totals; a trade still open at the end is not counted, as in the totals.
    /// </summary>
    public static double[] Cumulative(IReadOnlyList<StrategyTrade> trades, IReadOnlyList<int> points, Func<StrategyTrade, double>? percent = null)
    {
        percent ??= x => x.ProfitPercent;
        var closed = trades.Where(x => x.Outcome != EnumTradeOutcome.Open).OrderBy(x => x.ExitIndex).ToList();
        var sums = new double[points.Count];
        var (next, total) = (0, 0.0);
        for (var k = 0; k < points.Count; k++)
        {
            for (; next < closed.Count && (closed[next].ExitIndex <= points[k] || k == points.Count - 1); next++)
                total += percent(closed[next]);
            sums[k] = total;
        }

        return sums;
    }

    /// <summary>
    /// The candles a random run tries to enter at: each of the candles by the chance, from the seed.
    /// </summary>
    public static IEnumerable<(int Index, IReadOnlyList<int> Met)> Entries(int candles, double chance, int seed) => Entries(0, candles, chance, seed);

    /// <summary>
    /// The candles of a window, first up to but not including end, a random run tries to enter at: each by the chance, from
    /// the seed.
    /// </summary>
    public static IEnumerable<(int Index, IReadOnlyList<int> Met)> Entries(int first, int end, double chance, int seed)
    {
        var random = new Random(seed);
        for (var i = first; i < end; i++)
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

    internal static BaselineMeasure Measure(string name, double? strategy, IEnumerable<double> runs)
    {
        var sorted = runs.Order().ToList();
        double? percentile = strategy is double value && sorted.Count > 0
            ? 100 * (sorted.Count(x => x < value) + 0.5 * sorted.Count(x => x == value)) / sorted.Count
            : null;
        return new BaselineMeasure(name, strategy, sorted.Count > 0 ? sorted.Average() : 0, Quantile(sorted, 5), Quantile(sorted, 50),
            Quantile(sorted, 95), percentile);
    }
}
