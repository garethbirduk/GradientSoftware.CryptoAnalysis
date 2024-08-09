namespace Gradient.CryptoAnalysis.Conditions.DateConditions
{
    public abstract class DateCondition : ICondition, IDateCondition
    {
        public DateTime DateTimeCandidate { get; protected set; }
        public DateTime DateTimeCondition { get; protected set; }
        public bool IsExpired { get; set; }

        public abstract bool IsMet();

        public void SetDateTimeCandidate(DateTime dateTime)
        {
            DateTimeCandidate = dateTime;
        }
    }
}