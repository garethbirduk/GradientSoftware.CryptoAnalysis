using Gradient.CryptoAnalysis;

namespace Gradient.CryptoAnalysis.Conditions.RangeConditions
{
    public class IsUptrend : RangeCondition
    {
        public IsUptrend(int successiveCandles) : base(successiveCandles)
        {
        }

        public override bool IsMet()
        {
            if (IsExpired)
                return false;

            throw new NotImplementedException();
        }
    }
}