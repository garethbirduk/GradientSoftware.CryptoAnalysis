namespace Gradient.CryptoAnalysis;

public static class ListPriceExtensions
{
    public static Price? SwingHigh(this IEnumerable<Price> prices, EnumCloseType close)
    {
        return prices.FirstOrDefault(x => x.CloseValue(close) == prices.Max(x => x.CloseValue(close)));
    }
}
