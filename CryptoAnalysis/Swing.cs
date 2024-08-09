using PostSharp.Patterns.Contracts;

namespace Gradient.CryptoAnalysis
{
    public class Swing
    {
        public Swing([Required] IEnumerable<Price> prices, Swing? previousSwing, Price? nextPrice)
        {
            Prices = prices.ToList();
            PreviousSwing = previousSwing;
            NextPrice = nextPrice;
        }

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
                if (PreviousSwing == null)
                    return null;
                return Prices.FirstOrDefault(x => x.Close < PreviousSwing.SwingLow.Close);
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

        public List<Swing> InterimSwings
        {
            get
            {
                return Prices.ToList().ToSwings();
            }
        }

        public Price? NextPrice { get; }
        public Price? PreviousHigh { get; }
        public Price? PreviousLow { get; }
        public Swing? PreviousSwing { get; }
        public List<Price> Prices { get; set; } = new();

        public Price SwingLow
        {
            get
            {
                return Prices.First(x => x.Close == Prices.Min(x => x.Close));
            }
        }
    }
}