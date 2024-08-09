namespace Gradient.CryptoAnalysis
{
    public interface ICondition
    {
        public bool IsExpired { get; set; }

        public bool IsMet();
    }
}