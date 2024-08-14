namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsStartOfRangeCondition : PriceCondition
    {
        protected override bool IsMet()
        {
            if (IsExpired)
                return false;
            return false;
        }
    }
}