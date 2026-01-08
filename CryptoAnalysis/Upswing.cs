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

        public List<Price> DownlegPrices(EnumCloseType closeType, bool includeFirstPrice, bool includeSwingLow)
        {
            var swingLow = SwingLow(closeType);
            if (swingLow == null)
                return new List<Price>();
            var skip = includeFirstPrice ? 0 : 1;
            var downleg = Prices.Skip(skip).Where(x => x.DateTime < swingLow.DateTime).ToList();
            if (includeSwingLow)
            {
                var swinglow = SwingLow(closeType);
                if (swinglow != null)
                    downleg.Add(swinglow);
            }
            return downleg;
        }

        public Downswing? DownlegSwing(EnumCloseType closeType)
        {
            return DownlegPrices(closeType, true, true).ToDownswings(closeType, false).SingleOrDefault();
        }

        public List<Downswing> InterimDownswings(EnumCloseType closeType, bool includeFirstPrice, bool includeSwingLow)
        {
            var list = new List<Price>();
            return DownlegPrices(closeType, includeFirstPrice, includeSwingLow).Union(list).ToList().ToDownswings(closeType);
        }

        public List<Upswing> InterimUpswings(EnumCloseType closeType, bool includeSwingLow, bool includeNextPrice)
        {
            var list = new List<Price>();
            return UplegPrices(closeType, includeSwingLow, includeNextPrice).Union(list).ToList().ToUpswings(closeType);
        }

        public Price? SwingHigh(EnumCloseType close)
        {
            var s = Prices.FirstOrDefault(x => x.CloseValue(close) == Prices.Max(x => x.CloseValue(close)));
            return s;
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

        public List<Price> UplegPrices(EnumCloseType closeType, bool includeSwingLow, bool includeNextPrice)
        {
            var swingLow = SwingLow(closeType);
            if (swingLow == null)
                return new List<Price>();
            var skip = includeSwingLow ? 0 : 1;
            var upleg = Prices.Where(x => x.DateTime >= swingLow.DateTime).Skip(skip).ToList();
            if (includeNextPrice && NextPrice != null)
                upleg.Add(NextPrice);
            return upleg;
        }

        //public List<Upswing> UplegSwing(EnumCloseType closeType)
        //{
        //    var Prices = UplegPrices(closeType);
        //    var upswings = Prices.ToUpswings(closeType);
        //    return upswings;
        //}
    }

    public class UpwardBreakout
    {
        public UpwardBreakout([Required] IEnumerable<Price> prices, Upswing upswing, EnumCloseType closeType = EnumCloseType.Close)
        {
            Prices = prices.Where(x => x != null).ToList();
            Upswing = upswing;

            var last = prices.LastOrDefault();
            if (last != null && Confirmation != null && last.DateTime > Confirmation.DateTime && last.CloseValue(closeType) > Confirmation.CloseValue(closeType))
                SuccessfulBreakoutPrices = prices.ToList();
            if (last != null && Confirmation != null && last.DateTime > Confirmation.DateTime && last.CloseValue(closeType) <= Confirmation.CloseValue(closeType))
                FailedBreakoutPrices = prices.ToList();
        }

        public Price? Breakout
        {
            get
            {
                return Upswing.BreakOfStructure;
            }
        }

        public Price? Confirmation
        {
            get
            {
                if (Breakout == null)
                    return null;

                return Prices.Skip(1).FirstOrDefault();
            }
        }

        public List<Price> FailedBreakout
        {
            get
            {
                var list = new List<Price>();
                if (Confirmation != null)
                    list.AddRange(FailedBreakoutPrices.Skip(1));
                return list;
            }
        }

        public List<Price> FailedBreakoutPrices { get; } = [];
        public List<Price> Prices { get; set; } = [];

        public List<Price> SuccessfulBreakout
        {
            get
            {
                var list = new List<Price>();
                if (Confirmation != null)
                    list.AddRange(SuccessfulBreakoutPrices.Skip(1));
                return list;
            }
        }

        public List<Price> SuccessfulBreakoutPrices { get; } = [];
        public Upswing Upswing { get; }
    }
}