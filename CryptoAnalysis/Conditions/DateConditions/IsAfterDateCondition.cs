using CryptoAnalysis.Conditions.DateConditions;

namespace Gradient.CryptoAnalysis
{
    public class IsAfterDateCondition : DateCondition
    {
        public IsAfterDateCondition(DateTime dateTimeCondition)
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

            return DateTimeCandidate > DateTimeCondition;
        }
    }
}