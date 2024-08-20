using Newtonsoft.Json;
using PostSharp.Patterns.Contracts;

namespace Gradient.CryptoAnalysis
{
    public class Downswing
    {
        public Downswing([Required] IEnumerable<Price> prices, Downswing? previousDownswing, Price? nextPrice)
        {
            Prices = prices.Where(x => x != null).ToList();
            PreviousDownswing = previousDownswing;
            NextPrice = nextPrice;
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

        public List<Downswing> InterimDownswings
        {
            get
            {
                return Prices.Skip(1).Union(new List<Price>() { NextPrice }).ToList().ToDownswings();
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

        public Price? MarketStructureBreak
        {
            get
            {
                if (PreviousDownswing == null)
                    return null;
                if (PreviousDownswing.SwingHigh == null)
                    return null;
                return Prices.FirstOrDefault(x => x.Close > PreviousDownswing.SwingHigh.Close);
            }
        }

        public List<Price> NextInterswingPrices { get; set; } = new List<Price>();
        public Price? NextPrice { get; }
        public Downswing? PreviousDownswing { get; }
        public Price? PreviousHigh { get; }
        public List<Price> PreviousInterswingPrices { get; set; } = new List<Price>();
        public Price? PreviousLow { get; }
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

        public Price? SwingLow
        {
            get
            {
                return Prices.FirstOrDefault(x => x.Close == Prices.Min(x => x.Close));
            }
        }

        public Price? SwingOpen
        {
            get
            {
                return Prices.First();
            }
        }
    }
}