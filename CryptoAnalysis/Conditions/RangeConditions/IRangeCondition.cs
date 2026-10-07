namespace Gradient.CryptoAnalysis
{
    public interface IRangeCondition
    {
        public List<Price> Highs { get; }
        public List<Price> Lows { get; }

        public void SetPrice(DateTime dateTime);

        public void SetPrice(int index);

        public void SetPrices(List<Price> prices);
    }
}