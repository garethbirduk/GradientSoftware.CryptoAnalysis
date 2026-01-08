using Newtonsoft.Json;
using PostSharp.Patterns.Contracts;

namespace Gradient.CryptoAnalysis;

public class Upleg
{
    public List<Price> Prices { get; private set; } = [];

    public void Setup([Required] Downswing downswing, EnumCloseType closeType, bool includeFirstPrice, bool includeSwingHigh)
    {
        Prices = [];

        var swingHigh = downswing.SwingHigh(closeType);
        if (swingHigh == null)
            return;

        var skip = includeFirstPrice ? 0 : 1;
        Prices = downswing.Prices.Where(x => x.DateTime < swingHigh.DateTime).ToList();

        if (includeSwingHigh && swingHigh != null)
            Prices.Add(swingHigh);
    }

    public void Setup([Required] Upswing upswing, EnumCloseType closeType, bool includeFirstPrice, bool includeSwingLow)
    {
        Prices = [];

        var swingLow = upswing.SwingLow(closeType);
        if (swingLow == null)
            return;

        var skip = includeFirstPrice ? 0 : 1;
        Prices = upswing.Prices.Where(x => x.DateTime < swingLow.DateTime).ToList();

        if (includeSwingLow && swingLow != null)
            Prices.Add(swingLow);
    }
}