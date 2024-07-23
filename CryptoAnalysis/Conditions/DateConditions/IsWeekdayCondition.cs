using CryptoAnalysis.Conditions.DateConditions;

namespace Gradient.CryptoAnalysis
{
    public class IsWeekdayCondition : DateCondition
    {
        public override bool IsMet()
        {
            if (IsExpired)
                return false;

            return DateTimeCandidate.DayOfWeek != DayOfWeek.Saturday && DateTimeCandidate.DayOfWeek != DayOfWeek.Sunday;
        }
    }
}