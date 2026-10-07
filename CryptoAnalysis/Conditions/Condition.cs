using CryptoAnalysis.Conditions;

namespace Gradient.CryptoAnalysis.Conditions
{
    public abstract class Condition : ICondition
    {
        protected abstract bool IsMet();

        public bool IsExpired { get; set; }

        public bool IsMet(bool allowExpired)
        {
            if (IsExpired && !allowExpired)
                return false;

            return IsMet();
        }
    }
}