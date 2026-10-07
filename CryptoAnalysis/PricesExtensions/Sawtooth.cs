namespace Gradient.CryptoAnalysis;

public static partial class PricesExtensions_Sawtooth
{
    public static List<Price> ToDownwardSawtooth(this List<Price> prices, EnumCloseType closeType, bool includeFirst = false)
    {
        var downswings = prices.ToDownswings(closeType, false);
        var lows = downswings.Select(x => x.Prices.First());
        var highs = downswings.Select(x => x.SwingHigh(closeType));
        var sawtooth = highs.Union(lows).OrderBy(x => x.DateTime).ToList();

        if (includeFirst && sawtooth.FirstOrDefault() != prices.FirstOrDefault())
            sawtooth.Insert(0, prices.First());
        return sawtooth;
    }

    public static List<Price> ToUpwardSawtooth(this List<Price> prices, EnumCloseType closeType, bool includeFirst = false)
    {
        var upswings = prices.ToUpswings(closeType, false);
        var highs = upswings.Select(x => x.Prices.First());
        var lows = upswings.Select(x => x.SwingLow(closeType));
        var sawtooth = highs.Union(lows).OrderBy(x => x.DateTime).ToList();

        if (includeFirst && sawtooth.FirstOrDefault() != prices.FirstOrDefault())
            sawtooth.Insert(0, prices.First());
        return sawtooth;
    }
}