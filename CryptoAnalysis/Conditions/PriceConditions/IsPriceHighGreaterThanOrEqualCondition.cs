namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsPriceHighGreaterThanOrEqualCondition : TargetPriceCondition
    {
        protected override bool IsMet()
        {
            return Price.High >= TargetPrice;
        }

        public IsPriceHighGreaterThanOrEqualCondition(double targetPrice) : base(1)
        {
            TargetPrice = targetPrice;
        }
    }
}