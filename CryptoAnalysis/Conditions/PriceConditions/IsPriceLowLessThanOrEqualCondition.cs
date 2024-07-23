namespace Gradient.CryptoAnalysis
{
    public class IsPriceLowLessThanOrEqualCondition : PriceCondition
    {
        public IsPriceLowLessThanOrEqualCondition(double targetPrice) : base(1)
        {
            TargetPrice = targetPrice;
        }

        public double TargetPrice { get; }

        public override bool IsMet()
        {
            return Price.Low <= TargetPrice;
        }
    }
}