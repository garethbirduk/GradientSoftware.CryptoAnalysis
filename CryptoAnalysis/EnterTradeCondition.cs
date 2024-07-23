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

    public abstract class EnterTradeCondition : ICondition
    {
        public List<Price> Data { get; set; } = [];
        public DateTime DateTime { get; set; }
        public bool IsExpired { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public abstract bool IsMet(int index);

        public virtual bool IsMet()
        {
            var item = Data.SingleOrDefault(x => x.Time == DateTime);
            if (item == null)
                return false;

            return IsMet(Data.IndexOf(item));
        }
    }
}