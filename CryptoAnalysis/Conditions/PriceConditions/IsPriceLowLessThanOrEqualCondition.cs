namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsPriceLowLessThanOrEqualCondition : PriceCondition
    {
        public IsPriceLowLessThanOrEqualCondition(double targetPrice) : base(1)
        {
            TargetPrice = targetPrice;
        }

        public double TargetPrice { get; protected set; }

        public override bool IsMet()
        {
            return Price.Low <= TargetPrice;
        }

        public void SetTargetPrice(double targetPrice)
        {
            TargetPrice = targetPrice;
        }
    }
}