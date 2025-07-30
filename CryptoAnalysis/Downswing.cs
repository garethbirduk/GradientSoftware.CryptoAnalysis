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
                return Prices.Skip(1).Union(new List<Price>() { NextPrice }).ToList().ToDownswings(EnumCloseType.Low);
            }
        }

        public List<Price> Interimhighs
        {
            get
            {
                return Prices.Skip(1).LowClosesIsRed(EnumCloseType.Low);
            }
        }

        public List<Price> Interimlows
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
                if (PreviousDownswing.Swinglow == null)
                    return null;
                return Prices.FirstOrDefault(x => x.CloseValue(EnumCloseType.Close) > PreviousDownswing.Swinglow(EnumCloseType.Close).CloseValue(EnumCloseType.Close));
            }
        }

        public List<Price> NextInterswingPrices { get; set; } = new List<Price>();

        public Price? NextPrice { get; }

        public Downswing? PreviousDownswing { get; }
        public Price? Previoushigh { get; }

        public List<Price> PreviousInterswingPrices { get; set; } = new List<Price>();

        public Price? Previouslow { get; }
        public List<Price> Prices { get; set; } = new();

        public Price? SwingClose
        {
            get
            {
                return Prices.Last();
            }
        }

        public Price? SwingOpen
        {
            get
            {
                return Prices.First();
            }
        }

        public Price? SwingHigh(EnumCloseType close)
        {
            return Prices.FirstOrDefault(x => x.CloseValue(close) == Prices.Max(x => x.CloseValue(close)));
        }

        public Price? Swinglow(EnumCloseType close)
        {
            return Prices.FirstOrDefault(x => x.CloseValue(close) == Prices.Min(x => x.CloseValue(close)));
        }

        public override string ToString()
        {
            return $"{InitialPrice}-{BreakOfStructure} ({Prices.Count()})";
        }
    }

    public class Downswingx
    {
        public Price InitialPrice { get; set; } = new();
        public List<Price> Prices { get; set; } = new();
    }
}