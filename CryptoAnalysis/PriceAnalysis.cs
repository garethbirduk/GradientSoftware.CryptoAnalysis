using PostSharp.Patterns.Contracts;

namespace Gradient.CryptoAnalysis
{
    public enum Trend
    {
        Uptrend,
        Downtrend,
        Range
    }

    public static class ListListPriceExtensions
    {
        public static List<List<Price>> ConcatenateSegments(this List<List<Price>> segments)
        {
            var list = new List<List<Price>>()
            {
                segments.First()
            };

            foreach (var segment in segments.Skip(1))
            {
                if (segment.Count < 2)
                {
                    list.Last().AddRange(segment);
                }
                else
                {
                    list.Add(segment);
                }
            }
            return list;
        }
    }

    public class Swing
    {
        public Swing([Required] IEnumerable<Price> prices, Swing? previousSwing, Price? nextPrice)
        {
            Prices = prices.ToList();
            PreviousSwing = previousSwing;
            NextPrice = nextPrice;
        }

        public Price? ConfirmedBreakOfStructurePrice
        {
            get
            {
                var price = Prices.FirstOrDefault(x => x.Close > InitialPrice.Close);
                if (price == null && NextPrice != null)
                    return NextPrice;
                if (price == null)
                    return null;
                if (price.Close > InitialPrice.Close)
                    return price;
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

        public List<Swing> Swings
        {
            get
            {
                return Prices.Skip(1).ToList().ToSwings();
            }
        }
    }
}