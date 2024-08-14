using Gradient.CryptoAnalysis.Conditions;

namespace Gradient.CryptoAnalysis
{
    public class BacktestConditionRules
    {
        public ConditionSet ConfirmationConditions { get; set; } = new ConditionSet();
        public ConditionSet ExpireConditions { get; set; } = new ConditionSet();
        public ConditionSet PreConditions { get; set; } = new ConditionSet();
        public ConditionSet StopLossConditions { get; set; } = new ConditionSet();
        public ConditionSet TakeProfitConditions { get; set; } = new ConditionSet();
    }
}