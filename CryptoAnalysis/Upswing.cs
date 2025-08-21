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
                return Prices.FirstOrDefault(x => x.CloseValue(EnumCloseType.Close) < PreviousUpswing.SwingLow(EnumCloseType.Close).CloseValue(EnumCloseType.Close));
            }
        }

        public Price? NextPrice { get; }

        public Upswing? PreviousUpswing { get; }

        public List<Price> Prices { get; set; } = new();

        public List<Upswing> InterimUpswings(EnumCloseType closeType)
        {
            if (NextPrice != null)
            {
                var upswings = Prices.Skip(1).Union(new List<Price>() { NextPrice }).ToList().ToUpswings(closeType);
                return upswings;
            }
            return Prices.Skip(1).Union(new List<Price>()).ToList().ToUpswings(closeType);
        }

        public Price? SwingLow(EnumCloseType close)
        {
            return Prices.FirstOrDefault(x => x.CloseValue(close) == Prices.Min(x => x.CloseValue(close)));
        }

        public override string ToString()
        {
            return $"{InitialPrice}-{BreakOfStructure} ({Prices.Count()})";
        }
    }
}