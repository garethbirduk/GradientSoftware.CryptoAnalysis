using Gradient.CryptoAnalysis;
using Gradient.CryptoAnalysis.Conditions;

namespace CryptoAnalysis
{
    public class PositionRules
    {
        internal bool IsMet(List<Price> data, DateTime dateTime)
        {
            return Conditions.IsMet(data, dateTime);
        }

        public Condition Conditions { get; set; } = new Condition();
    }
}