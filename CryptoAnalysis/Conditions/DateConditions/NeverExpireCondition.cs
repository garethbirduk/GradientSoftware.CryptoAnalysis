using Gradient.CryptoAnalysis;
using Gradient.CryptoAnalysis.Conditions;

namespace CryptoAnalysis.Conditions.DateConditions
{
    public class NeverExpireCondition : Condition, ICondition
    {
        public bool IsExpired { get; set; }

        public bool IsMet()
        {
            return false;
        }
    }
}