using CryptoAnalysis.Conditions;

namespace Gradient.CryptoAnalysis.Conditions.DateConditions
{
    public abstract class NeverExpireCondition : Condition, ICondition
    {
        protected override bool IsMet()
        {
            return false;
        }

        public new bool IsMet(bool allowExpired)
        {
            return IsMet();
        }
    }
}