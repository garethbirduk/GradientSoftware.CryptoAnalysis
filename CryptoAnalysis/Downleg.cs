using Newtonsoft.Json;
using PostSharp.Patterns.Contracts;

namespace Gradient.CryptoAnalysis;

public class Downleg
{
    private Downleg()
    { }

    public List<Price> Prices { get; private set; } = [];

    public static Downleg Create(List<Price> prices)
    {
        return new Downleg()
        {
            Prices = prices
        };
    }

    public static Downleg Create([Required] Downswing downswing, EnumCloseType closeType, bool includeSwingHigh, bool includeNextPrice)
    {
        var swingHigh = downswing.SwingHigh(closeType);
        if (swingHigh == null)
            return new Downleg();

        var skip = includeSwingHigh ? 0 : 1;
        var prices = downswing.Prices.Where(x => x.DateTime >= swingHigh.DateTime).Skip(skip).ToList();

        if (includeNextPrice)
        {
            var nextPrice = downswing.NextPrice;
            if (nextPrice != null && !prices.Contains(nextPrice))
                prices.Add(nextPrice);
        }

        return new Downleg()
        {
            Prices = prices
        };
    }

    public static Downleg Create([Required] Upswing upswing, EnumCloseType closeType, bool includeFirstPrice, bool includeSwingLow)
    {
        var swingLow = upswing.SwingLow(closeType);
        if (swingLow == null)
            return new Downleg();

        var skip = includeFirstPrice ? 0 : 1;
        var prices = upswing.Prices.Where(x => x.DateTime <= swingLow.DateTime).Skip(skip).ToList();

        if (includeSwingLow && swingLow != null && !prices.Contains(swingLow))
            prices.Add(swingLow);

        return new Downleg()
        {
            Prices = prices
        };
    }
}