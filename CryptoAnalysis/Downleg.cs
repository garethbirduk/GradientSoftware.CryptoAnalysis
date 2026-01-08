using Newtonsoft.Json;
using PostSharp.Patterns.Contracts;

namespace Gradient.CryptoAnalysis;

public class Downleg
{
    public List<Price> Prices { get; private set; } = [];

    public void Setup([Required] Downswing downswing, EnumCloseType closeType, bool includeSwingHigh, bool includeNextPrice)
    {
        Prices = new List<Price>();

        var swingHigh = downswing.SwingHigh(closeType);
        if (swingHigh == null)
            return;

        var skip = includeSwingHigh ? 0 : 1;
        Prices = downswing.Prices.Where(x => x.DateTime >= swingHigh.DateTime).Skip(skip).ToList();
        if (includeNextPrice && downswing.NextPrice != null)
            Prices.Add(downswing.NextPrice);
    }
}
