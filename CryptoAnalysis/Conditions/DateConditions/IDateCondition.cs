namespace Gradient.CryptoAnalysis.Conditions.DateConditions
{
    public interface IDateCondition
    {
        public DateTime DateTimeCandidate { get; }
        public DateTime DateTimeCondition { get; }

        public void SetDateTimeCandidate(DateTime dateTime);
    }
}