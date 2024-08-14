namespace Gradient.CryptoAnalysis.Conditions.DateConditions
{
    public class IsDateOnCondition : DateCondition
    {
        protected override bool IsMet()
        {
            return DateTimeConditions.Contains(DateTimeCandidate);
        }

        public IsDateOnCondition(params DateTime[] dateTimeConditions)
        {
            DateTimeConditions = dateTimeConditions.ToList();
        }

        public List<DateTime> DateTimeConditions { get; set; }

        public void Expire()
        {
            IsExpired = true;
        }
    }
}