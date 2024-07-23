namespace CryptoAnalysis.Conditions.DateConditions
{
    public class IsDateOnCondition : DateCondition
    {
        public IsDateOnCondition(params DateTime[] dateTimeConditions)
        {
            DateTimeConditions = dateTimeConditions.ToList();
        }

        public List<DateTime> DateTimeConditions { get; set; }

        public void Expire()
        {
            IsExpired = true;
        }

        public override bool IsMet()
        {
            if (IsExpired)
                return false;

            return DateTimeConditions.Contains(DateTimeCandidate);
        }
    }
}