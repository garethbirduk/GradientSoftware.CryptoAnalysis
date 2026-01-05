namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
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