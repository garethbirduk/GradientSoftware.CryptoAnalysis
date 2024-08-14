namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsPriceLowLessThanOrEqualCondition : TargetPriceCondition
    {
        protected override bool IsMet()
        {
            return Price.Low <= TargetPrice;
        }

        public IsPriceLowLessThanOrEqualCondition(double targetPrice) : base(1)
        {
            TargetPrice = targetPrice;
        }
    }
}