namespace Gradient.CryptoAnalysis.Research;

/// <summary>
/// Which way the price went first from a candle's close: up by the size before down by it, the other way, neither within the
/// candles allowed, or both in one candle, which does not say which came first.
/// </summary>
public enum EnumFirstTouch
{
    Up,
    Down,
    Neither,
    Both,
}

/// <summary>
/// What followed a condition: the times it was met (as events, counted one at a time: one met while the last one's outcome
/// was still open is not counted, as one trade at a time), how many went up first, down first, neither or both, and the
/// same for every candle of the period, the base the condition is set against. UpRate and BaseRate are the share up first of
/// those that went one way. Z is how far UpRate is from BaseRate in standard errors, and Score that as the share of random
/// samples of candles it beats, 0 to 100, as a baseline's percentile: near 100, up first more often than chance; near 0,
/// down first more often.
/// </summary>
public sealed record ConditionStat(int Events, int Up, int Down, int Neither, int Both, int BaseUp, int BaseDown)
{
    public double UpRate => Up + Down > 0 ? (double)Up / (Up + Down) : 0;

    public double BaseRate => BaseUp + BaseDown > 0 ? (double)BaseUp / (BaseUp + BaseDown) : 0.5;

    public double Edge => UpRate - BaseRate;

    public double Z => Up + Down > 0 && BaseRate is > 0 and < 1 ? Edge / Math.Sqrt(BaseRate * (1 - BaseRate) / (Up + Down)) : 0;

    public double Score => 100 * Explorer.NormalCdf(Z);

    /// <summary>
    /// Several periods' counts as one.
    /// </summary>
    public static ConditionStat Pool(IEnumerable<ConditionStat> stats) => stats.Aggregate(new ConditionStat(0, 0, 0, 0, 0, 0, 0), (a, b) =>
        new ConditionStat(a.Events + b.Events, a.Up + b.Up, a.Down + b.Down, a.Neither + b.Neither, a.Both + b.Both, a.BaseUp + b.BaseUp, a.BaseDown + b.BaseDown));
}

/// <summary>
/// Measures conditions directly, before any strategy is built on them: after a condition, does the price go up a size
/// before it goes down by it more or less often than after any candle? One pass over the prices for each condition and size.
/// </summary>
public static class Explorer
{
    /// <summary>
    /// For each candle, which way the price went first from its close, by percent either way, within the next candles: a
    /// candle that opens beyond a side has gone that way; one whose high and low reach both is Both.
    /// </summary>
    public static (EnumFirstTouch Touch, int At)[] FirstTouch(IReadOnlyList<Price> prices, double percent, int horizon)
    {
        var touches = new (EnumFirstTouch, int)[prices.Count];
        for (var i = 0; i < prices.Count; i++)
        {
            var (up, down) = (prices[i].Close * (1 + percent / 100), prices[i].Close * (1 - percent / 100));
            var last = Math.Min(i + horizon, prices.Count - 1);
            touches[i] = (EnumFirstTouch.Neither, last);
            for (var j = i + 1; j <= last; j++)
            {
                var p = prices[j];
                var touch = p.Open >= up ? EnumFirstTouch.Up
                    : p.Open <= down ? EnumFirstTouch.Down
                    : p.High >= up && p.Low <= down ? EnumFirstTouch.Both
                    : p.High >= up ? EnumFirstTouch.Up
                    : p.Low <= down ? EnumFirstTouch.Down
                    : EnumFirstTouch.Neither;
                if (touch == EnumFirstTouch.Neither)
                    continue;
                touches[i] = (touch, j);
                break;
            }
        }

        return touches;
    }

    /// <summary>
    /// What followed a condition over a window of the prices, first up to but not including end, against what followed every
    /// candle there. The condition may read the candles before the window, and an outcome may run past its end.
    /// </summary>
    public static ConditionStat Measure(bool[] met, (EnumFirstTouch Touch, int At)[] touches, (int First, int End) window)
    {
        var (events, up, down, neither, both, baseUp, baseDown) = (0, 0, 0, 0, 0, 0, 0);
        var free = window.First;
        for (var i = window.First; i < window.End; i++)
        {
            var touch = touches[i].Touch;
            if (touch == EnumFirstTouch.Up)
                baseUp++;
            else if (touch == EnumFirstTouch.Down)
                baseDown++;
            if (!met[i] || i < free)
                continue;

            events++;
            free = touches[i].At;
            switch (touch)
            {
                case EnumFirstTouch.Up: up++; break;
                case EnumFirstTouch.Down: down++; break;
                case EnumFirstTouch.Both: both++; break;
                default: neither++; break;
            }
        }

        return new ConditionStat(events, up, down, neither, both, baseUp, baseDown);
    }

    /// <summary>
    /// How likely a result as far from chance as a score is, either way, once tests results like it have been looked at: the
    /// chance that the most extreme of that many tests of nothing would be at least as extreme. Under 5% is beyond what
    /// chance gives, allowing for every test run.
    /// </summary>
    public static double Corrected(double score, int tests)
    {
        var p = 2 * Math.Min(score, 100 - score) / 100;
        return 1 - Math.Pow(1 - Math.Min(1, p), Math.Max(1, tests));
    }

    /// <summary>
    /// The standard normal distribution's cumulative probability at z.
    /// </summary>
    public static double NormalCdf(double z)
    {
        // Abramowitz and Stegun's 7.1.26, to within about 1e-7.
        var x = Math.Abs(z) / Math.Sqrt(2);
        var t = 1 / (1 + 0.3275911 * x);
        var erf = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return z >= 0 ? (1 + erf) / 2 : (1 - erf) / 2;
    }
}
