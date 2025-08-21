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

        public Price? MarketStructureBreak
        {
            get
            {
                if (PreviousDownswing == null)
                    return null;
                if (PreviousDownswing.SwingHigh == null)
                    return null;
                return Prices.FirstOrDefault(x => x.CloseValue(EnumCloseType.Close) > PreviousDownswing.SwingHigh(EnumCloseType.Close).CloseValue(EnumCloseType.Close));
            }
        }

        public Price? NextPrice { get; }

        public Downswing? PreviousDownswing { get; }

        public List<Price> Prices { get; set; } = new();

        public List<Downswing> InterimDownswings(EnumCloseType closeType)
        {
            if (NextPrice != null)
            {
                var downswings = Prices.Skip(1).Union(new List<Price>() { NextPrice }).ToList().ToDownswings(closeType);
                return downswings;
            }
            return Prices.Skip(1).Union(new List<Price>()).ToList().ToDownswings(closeType);
        }

        public Price? SwingHigh(EnumCloseType close)
        {
            return Prices.FirstOrDefault(x => x.CloseValue(close) == Prices.Max(x => x.CloseValue(close)));
        }

        public override string ToString()
        {
            return $"{InitialPrice}-{BreakOfStructure} ({Prices.Count()})";
        }
    }
}