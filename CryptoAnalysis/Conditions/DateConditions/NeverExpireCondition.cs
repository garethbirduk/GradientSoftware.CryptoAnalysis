using Gradient.CryptoAnalysis;
using Gradient.CryptoAnalysis.Conditions;

namespace Gradient.CryptoAnalysis.Conditions.DateConditions
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