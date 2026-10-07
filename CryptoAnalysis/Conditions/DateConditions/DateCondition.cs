using CryptoAnalysis.Conditions;

namespace Gradient.CryptoAnalysis.Conditions.DateConditions
{
    public abstract class DateCondition : Condition, ICondition, IDateCondition
    {
        public DateTime DateTimeCandidate { get; protected set; }
        public DateTime DateTimeCondition { get; protected set; }

        public void SetDateTimeCandidate(DateTime dateTime)
        {
            DateTimeCandidate = dateTime;
        }
    }
}