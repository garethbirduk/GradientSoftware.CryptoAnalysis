using Gradient.CryptoAnalysis.Conditions;

namespace Gradient.CryptoAnalysis
{
    public class ConditionRules
    {
        public Condition ConfirmationConditions { get; set; } = new Condition();
        public Condition ExpireConditions { get; set; } = new Condition();
        public Condition PreConditions { get; set; } = new Condition();
        public Condition StopLossConditions { get; set; } = new Condition();
        public Condition TakeProfitConditions { get; set; } = new Condition();
    }
}