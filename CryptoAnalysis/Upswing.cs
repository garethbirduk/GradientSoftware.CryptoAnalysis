using Newtonsoft.Json;
using PostSharp.Patterns.Contracts;

namespace Gradient.CryptoAnalysis
{
    public class Upswing : Swing
    {
        public Upswing([Required] IEnumerable<Price> prices, Upswing? previousUpswing, Price? nextPrice)
        {
            Prices = prices.Where(x => x != null).ToList();
            PreviousUpswing = previousUpswing;
            NextPrice = nextPrice;
        }

        public override Price? BreakOfStructure
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

        public override Price? MarketStructureBreak
        {
            get
            {
                var previousUpswing = PreviousUpswing;
                if (previousUpswing == null)
                    return null;

                var swingLow = previousUpswing.SwingLow(EnumCloseType.Close);
                if (swingLow == null)
                    return null;

                return Prices.FirstOrDefault(x => x.CloseValue(EnumCloseType.Close) < swingLow.CloseValue(EnumCloseType.Close));
            }
        }

        public Upswing? PreviousUpswing { get; }

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

        public List<Downswing> InterimDownswings(EnumCloseType closeType, bool includeFirstPrice, bool includeSwingLow)
        {
            var list = new List<Price>();
            return Downleg.Create(this, closeType, includeFirstPrice, includeSwingLow).Prices.Union(list).ToList().ToDownswings(closeType);
        }

        public List<Upswing> InterimUpswings(EnumCloseType closeType, bool includeSwingLow, bool includeNextPrice)
        {
            var list = new List<Price>();
            return Upleg.Create(this, closeType, includeSwingLow, includeNextPrice).Prices.Union(list).ToList().ToUpswings(closeType);
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

        public EnumSwingType SwingType(EnumCloseType closeType)
        {
            var down = Downleg.Create(this, closeType, false, false).Prices.Any();
            var up = Upleg.Create(this, closeType, false, true).Prices.Any();

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