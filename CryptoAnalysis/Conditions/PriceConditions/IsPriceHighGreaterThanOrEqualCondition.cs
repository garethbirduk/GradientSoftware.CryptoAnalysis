namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsPriceHighGreaterThanOrEqualCondition : PriceCondition
    {
        public IsPriceHighGreaterThanOrEqualCondition(double targetPrice) : base(1)
        {
            TargetPrice = targetPrice;
        }

        public double TargetPrice { get; protected set; }

        public override bool IsMet()
        {
            return Price.High >= TargetPrice;
        }

        public void SetTargetPrice(double targetPrice)
        {
            TargetPrice = targetPrice;
        }
    }
}