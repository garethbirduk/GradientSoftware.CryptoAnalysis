﻿namespace Gradient.CryptoAnalysis.Conditions.DateConditions
{
    public class IsWeekdayCondition : DateCondition
    {
        protected override bool IsMet()
        {
            return DateTimeCandidate.DayOfWeek != DayOfWeek.Saturday && DateTimeCandidate.DayOfWeek != DayOfWeek.Sunday;
        }
    }
}