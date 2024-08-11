using Newtonsoft.Json;
using PostSharp.Patterns.Contracts;

namespace Gradient.CryptoAnalysis
{
    public class Upswing
    {
        public Upswing([Required] IEnumerable<Price> prices, Upswing? previousUpswing, Price? nextPrice)
        {
            Prices = prices.ToList();
            PreviousUpswing = previousUpswing;
            NextPrice = nextPrice;
        }

        public Price? BreakOfStructurePrice { get; internal set; }

        public Price? ConfirmedBreakOfStructure
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

        public Price? ConfirmedMarketStructureBreak
        {
            get
            {
                if (PreviousUpswing == null)
                    return null;
                if (PreviousUpswing.SwingLow == null)
                    return null;
                return Prices.FirstOrDefault(x => x.Close < PreviousUpswing.SwingLow.Close);
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
                return Prices.Skip(1).HighCloses();
            }
        }

        public List<Price> InterimLows
        {
            get
            {
                return Prices.Skip(1).LowCloses();
            }
        }

        public List<Upswing> InterimUpswings
        {
            get
            {
                return Prices.ToList().ToSwings();
            }
        }

        public List<Price> NextInterswingPrices { get; set; } = new List<Price>();
        public Price? NextPrice { get; }
        public Price? PreviousHigh { get; }
        public List<Price> PreviousInterswingPrices { get; set; } = new List<Price>();
        public Price? PreviousLow { get; }
        public Upswing? PreviousUpswing { get; }
        public List<Price> Prices { get; set; } = new();

        public Price? SwingLow
        {
            get
            {
                return Prices.FirstOrDefault(x => x.Close == Prices.Min(x => x.Close));
            }
        }
    }
}