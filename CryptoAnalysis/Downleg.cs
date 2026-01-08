using Newtonsoft.Json;
using PostSharp.Patterns.Contracts;

namespace Gradient.CryptoAnalysis;

public class Downleg
{
    public List<Price> Prices { get; private set; } = [];

    public void Setup([Required] Downswing downswing, EnumCloseType closeType, bool includeSwingHigh, bool includeNextPrice)
    {
        Prices = [];

        var swingHigh = downswing.SwingHigh(closeType);
        if (swingHigh == null)
            return;

        var skip = includeSwingHigh ? 0 : 1;
        Prices = downswing.Prices.Where(x => x.DateTime >= swingHigh.DateTime).Skip(skip).ToList();
        if (includeNextPrice && downswing.NextPrice != null)
            Prices.Add(downswing.NextPrice);
    }

    public void Setup([Required] Upswing upswing, EnumCloseType closeType, bool includeSwingLow, bool includeNextPrice)
    {
        Prices = [];

        var swingLow = upswing.SwingLow(closeType);
        if (swingLow == null)
            return;

        var skip = includeSwingLow ? 0 : 1;
        Prices = upswing.Prices.Where(x => x.DateTime >= swingLow.DateTime).Skip(skip).ToList();
        if (includeNextPrice && upswing.NextPrice != null)
            Prices.Add(upswing.NextPrice);
    }
}