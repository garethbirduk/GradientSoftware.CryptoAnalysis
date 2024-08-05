using Gradient.CryptoAnalysis.Conditions.PriceConditions;

namespace Gradient.CryptoAnalysis
{
    public abstract class PriceCondition : ICondition, IPriceCondition
    {
        public readonly int MinDataSize = 2;

        public PriceCondition(int successiveCandles)
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

        public bool IsExpired { get; set; }
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