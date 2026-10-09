namespace Gradient.CryptoAnalysis.Research;

/// <summary>
/// A condition the explorer tests: whether something is true at the close of each candle, from that candle and the ones
/// before it alone, so it could have been known then. Group gathers conditions of a kind for the page.
/// </summary>
public sealed record Condition(string Id, string Name, string Group, Func<IReadOnlyList<Price>, bool[]> Test);

/// <summary>
/// The conditions the explorer has. Each looks only backwards: a run of candles, a close beyond the candles before it, a
/// close against their average, a candle's size against theirs, the day of the week.
/// </summary>
public static class ExploreConditions
{
    public static readonly IReadOnlyList<Condition> All =
    [
        .. new[] { 2, 3, 4, 5, 6 }.Select(n => new Condition($"green-{n}", $"{n} green candles in a row", "Candle runs", p => Run(p, green: true, n))),
        .. new[] { 2, 3, 4, 5, 6 }.Select(n => new Condition($"red-{n}", $"{n} red candles in a row", "Candle runs", p => Run(p, green: false, n))),
        new("high-24", "Close above the last 24 candles' highs", "Breakouts", p => Beyond(p, 24, high: true)),
        new("low-24", "Close below the last 24 candles' lows", "Breakouts", p => Beyond(p, 24, high: false)),
        new("high-168", "Close above the last 168 candles' highs", "Breakouts", p => Beyond(p, 168, high: true)),
        new("low-168", "Close below the last 168 candles' lows", "Breakouts", p => Beyond(p, 168, high: false)),
        new("above-168", "Close above the average of the last 168 closes", "Averages", p => AgainstAverage(p, 168, above: true)),
        new("below-168", "Close below the average of the last 168 closes", "Averages", p => AgainstAverage(p, 168, above: false)),
        new("big-24", "A candle over twice the average size of the last 24", "Candle size", p => Size(p, 24, larger: true)),
        new("small-24", "A candle under half the average size of the last 24", "Candle size", p => Size(p, 24, larger: false)),
        new("weekend", "A candle at the weekend (UTC)", "Time", p => p.Select(x => x.DateTime.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday).ToArray()),
    ];

    /// <summary>
    /// The candles at whose close a run of a colour reaches the length: once a run, as a strategy's Successive Candles enters.
    /// </summary>
    public static bool[] Run(IReadOnlyList<Price> prices, bool green, int length)
    {
        var met = new bool[prices.Count];
        var run = 0;
        for (var i = 0; i < prices.Count; i++)
        {
            run = (green ? prices[i].Close > prices[i].Open : prices[i].Close < prices[i].Open) ? run + 1 : 0;
            met[i] = run == length;
        }

        return met;
    }

    /// <summary>
    /// The candles that close above the highest high of the candles before them, or below the lowest low.
    /// </summary>
    public static bool[] Beyond(IReadOnlyList<Price> prices, int candles, bool high)
    {
        var met = new bool[prices.Count];
        for (var i = candles; i < prices.Count; i++)
        {
            var before = Enumerable.Range(i - candles, candles);
            met[i] = high ? prices[i].Close > before.Max(j => prices[j].High) : prices[i].Close < before.Min(j => prices[j].Low);
        }

        return met;
    }

    /// <summary>
    /// The candles that close above, or below, the average close of the candles up to and including them.
    /// </summary>
    public static bool[] AgainstAverage(IReadOnlyList<Price> prices, int candles, bool above)
    {
        var met = new bool[prices.Count];
        var sum = 0.0;
        for (var i = 0; i < prices.Count; i++)
        {
            sum += prices[i].Close;
            if (i >= candles)
                sum -= prices[i - candles].Close;
            if (i >= candles - 1)
                met[i] = above ? prices[i].Close > sum / candles : prices[i].Close < sum / candles;
        }

        return met;
    }

    /// <summary>
    /// The candles whose range, high to low, is over twice the average range of the candles before them, or under half it.
    /// </summary>
    public static bool[] Size(IReadOnlyList<Price> prices, int candles, bool larger)
    {
        var met = new bool[prices.Count];
        var sum = 0.0;
        for (var i = 0; i < prices.Count; i++)
        {
            var range = prices[i].High - prices[i].Low;
            if (i >= candles)
            {
                var average = sum / candles;
                met[i] = larger ? range > 2 * average : range < average / 2;
                sum -= prices[i - candles].High - prices[i - candles].Low;
            }

            sum += range;
        }

        return met;
    }
}
