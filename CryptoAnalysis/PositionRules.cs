using Gradient.CryptoAnalysis.Conditions;

namespace CryptoAnalysis
{
    public class PositionRules
    {
        public Condition ConfirmationConditions { get; set; } = new Condition();
        public Condition ExpireConditions { get; set; } = new Condition();
        public Condition PreConditions { get; set; } = new Condition();
        public Condition StopLossConditions { get; set; } = new Condition();
        public Condition TakeProfitConditions { get; set; } = new Condition();
    }
}