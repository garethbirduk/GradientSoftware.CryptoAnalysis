using Newtonsoft.Json;
using PostSharp.Patterns.Contracts;

namespace Gradient.CryptoAnalysis;

public class Downswing : Swing
{
    public Downswing([Required] IEnumerable<Price> prices, Downswing? previousDownswing, Price? nextPrice)
    {
        Prices = prices.Where(x => x != null).ToList();
        PreviousDownswing = previousDownswing;
        NextPrice = nextPrice;

        Downleg.Setup(this, EnumCloseType.Close, false, false);
        Upleg.Setup(this, EnumCloseType.Close, false, false);
    }

    public Price? BreakOfStructure
    {
        get
        {
            var price = Prices.FirstOrDefault(x => x.Close < InitialPrice.Close);

            if (price != null && price.Close < InitialPrice.Close)
                return price;
            if (price == null && NextPrice != null)
                return NextPrice;
            return null;
        }
    }

    public Price InitialPrice
    {
        get
        {
            return Prices.First();
        }
    }

    public Price? MarketStructureBreak
    {
        get
        {
            var previousDownswing = PreviousDownswing;
            if (previousDownswing == null)
                return null;

            var swingHigh = previousDownswing.SwingHigh(EnumCloseType.Close);
            if (swingHigh == null)
                return null;

            return Prices.FirstOrDefault(x => x.CloseValue(EnumCloseType.Close) > swingHigh.CloseValue(EnumCloseType.Close));
        }
    }

    public Price? NextPrice { get; set; }
    public Downswing? PreviousDownswing { get; }

    public List<Price> DownlegPrices(EnumCloseType closeType, bool includeSwingHigh, bool includeNextPrice)
    {
        var swingHigh = SwingHigh(closeType);
        if (swingHigh == null)
            return [];
        var skip = includeSwingHigh ? 0 : 1;
        var downleg = Prices.Where(x => x.DateTime >= swingHigh.DateTime).Skip(skip).ToList();
        if (includeNextPrice && NextPrice != null)
            downleg.Add(NextPrice);
        return downleg;
    }

    public List<Downswing> InterimDownswings(EnumCloseType closeType, bool includeSwingHigh, bool includeNextPrice)
    {
        var list = new List<Price>();
        return DownlegPrices(closeType, includeSwingHigh, includeNextPrice).Union(list).ToList().ToDownswings(closeType);
    }

    public List<Upswing> InterimUpswings(EnumCloseType closeType, bool includeFirstPrice, bool includeSwingHigh)
    {
        var list = new List<Price>();
        return UplegPrices(closeType, includeFirstPrice, includeSwingHigh).Union(list).ToList().ToUpswings(closeType);
    }

    public Price? SwingHigh(EnumCloseType close)
    {
        return Prices.SwingHigh(close);
    }

    public Price? SwingLow(EnumCloseType close)
    {
        return Prices.FirstOrDefault(x => x.CloseValue(close) == Prices.Min(x => x.CloseValue(close)));
    }

    public EnumSwingType SwingType()
    {
        var up = Upleg.Prices.Any();
        var down = Downleg.Prices.Any();

        switch (up, down)
        {
            case (up: true, down: false):
                return EnumSwingType.UplegOnly;

            case (up: false, down: true):
                return EnumSwingType.DownlegOnly;

            case (true, true):
                {
                    if (BreakOfStructure == null)
                        return EnumSwingType.PartialSwing;
                    return EnumSwingType.Swing;
                }
            case (false, false):
                return EnumSwingType.None;
        }
    }

    public override string ToString()
    {
        return $"{InitialPrice}-{BreakOfStructure} ({Prices.Count()})";
    }

    //public Upleg Upleg(EnumCloseType closeType, bool includeSwingHigh, bool includeNextPrice)
    //{
    //    var swingHigh = SwingHigh(closeType);
    //    if (swingHigh == null)
    //        return new List<Price>();
    //    var skip = includeSwingHigh ? 0 : 1;
    //    var downleg = Prices.Where(x => x.DateTime >= swingHigh.DateTime).Skip(skip).ToList();
    //    if (includeNextPrice && NextPrice != null)
    //        downleg.Add(NextPrice);
    //    return downleg;

    //    return new Upleg(Prices, closeType, includeSwingHigh, includeNextPrice);
    //}

    public List<Price> UplegPrices(EnumCloseType closeType, bool includeFirstPrice, bool includeSwingHigh)
    {
        var swingHigh = SwingHigh(closeType);
        if (swingHigh == null)
            return [];
        var skip = includeFirstPrice ? 0 : 1;
        var upleg = Prices.Where(x => x.DateTime <= swingHigh.DateTime).Skip(skip).ToList();
        if (includeSwingHigh)
        {
            var swinghigh = SwingHigh(closeType);
            if (swinghigh != null)
                upleg.Add(swinghigh);
        }
        return upleg;
    }
}