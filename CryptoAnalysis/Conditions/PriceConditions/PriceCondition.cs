namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public abstract class PriceCondition : Condition, IPriceCondition
    {
        public const int DefaultSuccessiveCandles = 100;

        public PriceCondition(int successiveCandles = DefaultSuccessiveCandles)
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

        public int MinDataSize { get; set; } = 2;
        public Price Price { get; protected set; } = new();
        public List<Price> Prices { get; protected set; } = new();
        public int SuccessiveCandles { get; protected set; }

        public void SetPrice(DateTime dateTime)
        {
            Price = Prices.First(x => x.DateTime == dateTime);
        }

        public void SetPrice(int index)
        {
            Price = Prices[index];
        }

        public virtual void SetPrices(List<Price> data, Cursor newCursor)
        {
            Prices = data;
            switch (newCursor)
            {
                case Cursor.First:
                    {
                        SetPriceToFirst();
                        break;
                    }
                case Cursor.Last:
                    {
                        SetPriceToLast();
                        break;
                    }
                case Cursor.None:
                default:
                    {
                        break;
                    }
            }
        }

        public void SetPriceToFirst()
        {
            Price = Prices.First();
        }

        public void SetPriceToLast()
        {
            Price = Prices.Last();
        }
    }
}