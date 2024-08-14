namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsStartOfRangeCondition : PriceCondition
    {
        public IsStartOfRangeCondition(int successiveCandles) : base(successiveCandles)
        {
        }

        public override bool IsMet()
        {
            if (IsExpired)
                return false;
            return false;
        }
    }
}