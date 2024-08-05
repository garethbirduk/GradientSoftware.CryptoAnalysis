namespace Gradient.CryptoAnalysis.Conditions.DateConditions
{
    public class IsWeekendCondition : DateCondition
    {
        public override bool IsMet()
        {
            if (IsExpired)
                return false;

            return DateTimeCandidate.DayOfWeek == DayOfWeek.Saturday || DateTimeCandidate.DayOfWeek == DayOfWeek.Sunday;
        }
    }
}