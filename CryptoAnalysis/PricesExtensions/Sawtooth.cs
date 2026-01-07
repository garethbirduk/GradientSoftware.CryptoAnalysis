namespace Gradient.CryptoAnalysis;

public static partial class PricesExtensions_Sawtooth
{
    public static List<Price> ToDownwardSawtooth(this List<Price> prices, EnumCloseType closeType)
    {
        var downswings = prices.ToDownswings(closeType, true);
        var lows = downswings.Select(x => x.Prices.First());
        var highs = downswings.Select(x => x.SwingHigh(closeType));
        return highs.Union(lows).OrderBy(x => x.DateTime).ToList();
    }

    public static List<Price> ToUpwardSawtooth(this List<Price> prices, EnumCloseType closeType)
    {
        var upswings = prices.ToUpswings(closeType, true);
        var highs = upswings.Select(x => x.Prices.First());
        var lows = upswings.Select(x => x.SwingLow(closeType));
        return highs.Union(lows).OrderBy(x => x.DateTime).ToList();
    }
}