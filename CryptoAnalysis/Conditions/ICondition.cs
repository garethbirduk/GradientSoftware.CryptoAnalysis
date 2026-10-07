namespace CryptoAnalysis.Conditions
{
    public interface ICondition
    {
        public bool IsExpired { get; set; }

        public bool IsMet(bool allowExpired);
    }
}