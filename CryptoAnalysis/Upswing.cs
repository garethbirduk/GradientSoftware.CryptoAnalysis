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

        public List<Price> InterimHighs
        {
            get
            {
                return Prices.Skip(1).HighClosesIsGreen(EnumCloseType.High);
            }
        }

        public List<Price> InterimLows
        {
            get
            {
                return Prices.Skip(1).LowCloses();
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

        public List<Price> NextInterswingPrices { get; set; } = new List<Price>();

        public Price? NextPrice { get; }

        public Price? PreviousHigh { get; }

        public List<Price> PreviousInterswingPrices { get; set; } = new List<Price>();

        public Price? PreviousLow { get; }

        public Upswing? PreviousUpswing { get; }

        public List<Price> Prices { get; set; } = new();

        public Price? SwingClose
        {
            get
            {
                return Prices.Last();
            }
        }

        public Price? SwingHigh
        {
            get
            {
                return Prices.FirstOrDefault(x => x.Close == Prices.Max(x => x.Close));
            }
        }

        public Price? SwingOpen
        {
            get
            {
                return Prices.First();
            }
        }

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

    public class Upswingx
    {
        public Price InitialPrice { get; set; } = new();
        public List<Price> Prices { get; set; } = new();
    }
}