namespace Gradient.CryptoAnalysis
{
    public class IsAfterDateCondition : DateCondition
    {
        protected override bool IsMet()
        {
            return DateTimeCandidate > DateTimeCondition;
        }

        public IsAfterDateCondition(DateTime dateTimeCondition)
        {
            DateTimeCondition = dateTimeCondition;
        }
    }
}