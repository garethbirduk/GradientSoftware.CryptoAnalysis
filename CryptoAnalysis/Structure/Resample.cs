namespace Gradient.CryptoAnalysis;

/// <summary>
/// Builds coarser candles from finer ones: hourly candles into 4-hour or daily ones, say.
/// </summary>
public static class Resample
{
    /// <summary>
    /// Groups the candles into spans of the given length counted from midnight UTC (so 4 hours gives candles at 00:00,
    /// 04:00 and so on), each with the first open, the highest high, the lowest low and the last close. A span with no
    /// candles is left out, and a span missing some of its candles is built from the ones it has.
    /// </summary>
    public static List<Price> To(IReadOnlyList<Price> prices, TimeSpan span)
    {
        var result = new List<Price>();
        Price? current = null;
        DateTime bucket = default;
        foreach (var p in prices.OrderBy(x => x.DateTime))
        {
            var start = new DateTime(p.DateTime.Ticks - p.DateTime.Ticks % span.Ticks, p.DateTime.Kind);
            if (current == null || start != bucket)
            {
                current = new Price { DateTime = start, Open = p.Open, High = p.High, Low = p.Low, Close = p.Close };
                result.Add(current);
                bucket = start;
                continue;
            }

            current.High = Math.Max(current.High, p.High);
            current.Low = Math.Min(current.Low, p.Low);
            current.Close = p.Close;
        }

        return result;
    }
}
