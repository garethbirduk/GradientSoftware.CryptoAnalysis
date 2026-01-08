using Newtonsoft.Json;
using PostSharp.Patterns.Contracts;

namespace Gradient.CryptoAnalysis;

public class Upleg
{
    public List<Price> Prices { get; private set; } = [];

    public static Upleg Create([Required] Downswing downswing, EnumCloseType closeType, bool includeFirstPrice, bool includeSwingHigh)
    {
        var swingHigh = downswing.SwingHigh(closeType);
        if (swingHigh == null)
            return new Upleg();

        var skip = includeFirstPrice ? 0 : 1;
        var prices = downswing.Prices.Where(x => x.DateTime < swingHigh.DateTime).ToList();

        if (includeSwingHigh && swingHigh != null && !prices.Contains(swingHigh))
            prices.Add(swingHigh);

        return new Upleg()
        {
            Prices = prices
        };
    }

    public static Upleg Create([Required] Upswing upswing, EnumCloseType closeType, bool includeSwingLow, bool includeNextPrice)
    {
        var swingLow = upswing.SwingLow(closeType);
        if (swingLow == null)
            return new Upleg();

        var skip = includeSwingLow ? 0 : 1;
        var prices = upswing.Prices.Where(x => x.DateTime > swingLow.DateTime).Skip(skip).ToList();

        if (includeNextPrice)
        {
            var nextPrice = upswing.NextPrice;
            if (nextPrice != null && !prices.Contains(nextPrice))
                prices.Add(nextPrice);
        }

        return new Upleg()
        {
            Prices = prices
        };
    }
}