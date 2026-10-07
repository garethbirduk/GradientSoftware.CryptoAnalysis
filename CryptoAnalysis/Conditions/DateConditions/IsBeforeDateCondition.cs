namespace Gradient.CryptoAnalysis.Conditions.DateConditions
{
    public class IsBeforeDateCondition : DateCondition
    {
        protected override bool IsMet()
        {
            return DateTimeCandidate < DateTimeCondition;
        }

        public IsBeforeDateCondition(DateTime dateTimeCondition)
        {
            DateTimeCondition = dateTimeCondition;
        }
    }
}