namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public abstract class PriceCondition : Condition, IPriceCondition
    {
        public const int DefaultAdditionalCandles = 100;

        public PriceCondition(int additionalCandles = DefaultAdditionalCandles)
        {
            AdditionalCandles = additionalCandles;
        }

        public int AdditionalCandles { get; protected set; }

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

        public void SetPrice(DateTime dateTime)
        {
            var price = Prices.FirstOrDefault(x => x.DateTime == dateTime);

            if (price != null)
                Price = price;

            if (Prices.Contains(Price))
            {
                // do nothing; use the current price.
            }
            else
            {
                throw new Exception("Can't find a suitable price");
            }
        }

        public void SetPrice(int index)
        {
            Price = Prices[index];
        }

        public virtual void SetPrices(List<Price> data, Cursor newCursor)
        {
            Prices = data.Where(x => x != null).TakeLast(AdditionalCandles + 1).ToList();
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

    public abstract class TargetPriceCondition : PriceCondition
    {
        public TargetPriceCondition(double targetPrice, int successiveCandles = DefaultAdditionalCandles) : base(successiveCandles)
        {
            TargetPrice = targetPrice;
        }

        public double TargetPrice { get; protected set; }

        public void SetTargetPrice(double targetPrice)
        {
            TargetPrice = targetPrice;
        }
    }
}