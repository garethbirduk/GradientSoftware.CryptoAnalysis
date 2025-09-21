using Newtonsoft.Json;
using PostSharp.Patterns.Contracts;

namespace Gradient.CryptoAnalysis
{
    public class Upswing
    {
        public Upswing([Required] IEnumerable<Price> prices, Upswing? previousUpswing, Price? nextPrice)
        {
            Prices = prices.Where(x => x != null).ToList();
            PreviousUpswing = previousUpswing;
            NextPrice = nextPrice;
        }

        public Price? BreakOfStructure
        {
            get
            {
                var price = Prices.FirstOrDefault(x => x.Close > InitialPrice.Close);

                if (price != null && price.Close > InitialPrice.Close)
                    return price;
                if (price == null && NextPrice != null)
                    return NextPrice;
                return null;
            }
        }

        public bool Broken => BreakOfStructure != null;

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
                if (PreviousUpswing == null)
                    return null;
                if (PreviousUpswing.SwingLow == null)
                    return null;
                return Prices.FirstOrDefault(x => x.CloseValue(EnumCloseType.Close) < PreviousUpswing.SwingLow(EnumCloseType.Close)?.CloseValue(EnumCloseType.Close));
            }
        }

        public Price? NextPrice { get; set; }

        public Upswing? PreviousUpswing { get; }

        public List<Price> Prices { get; set; } = new();

        public List<Price> DownlegPrices(EnumCloseType closeType)
        {
            var swingLow = SwingLow(closeType);
            if (swingLow == null)
                return new List<Price>();
            return Prices.Skip(1).Where(x => x.DateTime <= swingLow.DateTime).ToList();
        }

        public Downswing? DownlegSwing(EnumCloseType closeType)
        {
            return DownlegPrices(closeType).ToDownswings(closeType, false).SingleOrDefault();
        }

        public List<Downswing> InterimDownswings(EnumCloseType closeType, int skip = 1)
        {
            var list = new List<Price>();
            return DownlegPrices(closeType).Skip(skip).Union(list).ToList().ToDownswings(closeType);
        }

        public List<Upswing> InterimUpswings(EnumCloseType closeType, int skip = 1)
        {
            var list = new List<Price>();
            return UplegPrices(closeType).Skip(skip).Union(list).ToList().ToUpswings(closeType);
        }

        public Price? SwingLow(EnumCloseType close)
        {
            var s = Prices.FirstOrDefault(x => x.CloseValue(close) == Prices.Min(x => x.CloseValue(close)));
            return s;
        }

        public override string ToString()
        {
            return $"{InitialPrice}-{BreakOfStructure} ({Prices.Count()})";
        }

        public List<Price> UplegPrices(EnumCloseType closeType)
        {
            var swingLow = SwingLow(closeType);
            if (swingLow == null)
                return new List<Price>();
            return Prices.Skip(1).Where(x => x.DateTime >= swingLow.DateTime).ToList();
        }

        public List<Upswing> UplegSwing(EnumCloseType closeType)
        {
            var prices = UplegPrices(closeType);
            var upswings = prices.ToUpswings(closeType);
            return upswings;
        }
    }
}