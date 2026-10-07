namespace Gradient.CryptoAnalysis.Conditions.DateConditions
{
    public class IsWeekendCondition : DateCondition
    {
        protected override bool IsMet()
        {
            return DateTimeCandidate.DayOfWeek == DayOfWeek.Saturday || DateTimeCandidate.DayOfWeek == DayOfWeek.Sunday;
        }
    }
}