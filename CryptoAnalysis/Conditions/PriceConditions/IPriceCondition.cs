using Gradient.CryptoAnalysis;

namespace CryptoAnalysis.Conditions.PriceConditions
{
    public interface IPriceCondition
    {
        public Price Price { get; }
        public List<Price> Prices { get; }

        public void SetPrice(DateTime dateTime);

        public void SetPrice(int index);

        public void SetPrices(List<Price> prices);
    }
}