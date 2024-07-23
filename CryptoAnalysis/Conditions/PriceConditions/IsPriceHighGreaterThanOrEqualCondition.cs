namespace Gradient.CryptoAnalysis
{
    public class IsPriceHighGreaterThanOrEqualCondition : PriceCondition
    {
        public IsPriceHighGreaterThanOrEqualCondition(double targetPrice) : base(1)
        {
            TargetPrice = targetPrice;
        }

        public double TargetPrice { get; }

        public override bool IsMet()
        {
            return Price.High >= TargetPrice;
        }
    }
}