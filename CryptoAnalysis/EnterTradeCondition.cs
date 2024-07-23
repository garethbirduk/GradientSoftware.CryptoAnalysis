namespace Gradient.CryptoAnalysis
{
    public enum EntryState
    {
    }

    public interface ICondition
    {
        public bool IsExpired { get; set; }

        public bool IsMet();
    }
}