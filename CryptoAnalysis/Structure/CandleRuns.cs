namespace Gradient.CryptoAnalysis;

/// <summary>
/// A run of consecutive candles of one colour. Start is the first candle's open, End the last candle's close.
/// </summary>
public sealed record CandleRun(bool Green, int Length, PricePoint Start, PricePoint End);

/// <summary>
/// Finds runs of consecutive green candles (close above open) and red candles (close below open).
/// A candle that closes where it opened ends any run, as does a candle of the other colour.
/// </summary>
public static class CandleRuns
{
    /// <summary>
    /// Returns every maximal run of at least minLength candles of one colour, in time order.
    /// </summary>
    public static List<CandleRun> Runs(List<Price> prices, int minLength = 3)
    {
        var runs = new List<CandleRun>();
        var start = 0;
        for (var i = 1; i <= prices.Count; i++)
        {
            var sameColour = i < prices.Count && Colour(prices[i]) != 0 && Colour(prices[i]) == Colour(prices[start]);
            if (sameColour)
                continue;

            var length = i - start;
            if (Colour(prices[start]) != 0 && length >= minLength)
            {
                var first = prices[start];
                var last = prices[i - 1];
                runs.Add(new CandleRun(Colour(first) > 0, length,
                    new PricePoint(first.DateTime, first.Open), new PricePoint(last.DateTime, last.Close)));
            }
            start = i;
        }

        return runs;
    }

    private static int Colour(Price price) => price.IsGreen() ? 1 : price.IsRed() ? -1 : 0;
}
