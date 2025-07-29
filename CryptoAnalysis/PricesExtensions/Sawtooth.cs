namespace Gradient.CryptoAnalysis;

public static partial class PricesExtensions_Sawtooth
{
    public static List<Price> ToSawtooth(this List<Price> prices, EnumCloseType closeType)
    {
        var upswings = prices.ToUpswings(closeType);
        var highs = upswings.Select(x => x.Prices.First());
        var lows = upswings.Select(x => x.Prices.AllTimeLows(closeType).MinBy(x => x.CloseValue(closeType)));
        return highs.Union(lows).OrderBy(x => x.DateTime).ToList();
    }
}