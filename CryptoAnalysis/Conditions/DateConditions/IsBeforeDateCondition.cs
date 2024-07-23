namespace CryptoAnalysis.Conditions.DateConditions
{
    public class IsBeforeDateCondition : DateCondition
    {
        public IsBeforeDateCondition(DateTime dateTimeCondition)
        {
            DateTimeCondition = dateTimeCondition;
        }

        public void Expire()
        {
            IsExpired = true;
        }

        public override bool IsMet()
        {
            if (IsExpired)
                return false;

            return DateTimeCandidate < DateTimeCondition;
        }
    }
}