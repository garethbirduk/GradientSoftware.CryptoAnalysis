namespace Gradient.CryptoAnalysis
{
    public abstract class IsRangeCondition : ICondition, IRangeCondition
    {
        public readonly int MinDataSize = 2;

        public IsRangeCondition(int successiveCandles)
        {
            SuccessiveCandles = successiveCandles;
        }

        public int CurrentIndex
        {
            get
            {
                return Prices.IndexOf(Price);
            }
        }

        public List<Price> Highs { get; set; } = new();
        public bool IsExpired { get; set; }
        public List<Price> Lows { get; set; } = new();
        public Price Price { get; protected set; } = new();
        public List<Price> Prices { get; protected set; } = new();
        public int SuccessiveCandles { get; }

        public abstract bool IsMet();

        public void SetPrice(DateTime dateTime)
        {
            Price = Prices.First(x => x.DateTime == dateTime);
        }

        public void SetPrice(int index)
        {
            Price = Prices[index];
        }

        public virtual void SetPrices(List<Price> data)
        {
            Prices = data;
        }
    }
}