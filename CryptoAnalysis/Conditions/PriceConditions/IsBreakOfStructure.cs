namespace Gradient.CryptoAnalysis
{
    public class IsBreakOfStructure : PriceCondition
    {
        public IsBreakOfStructure(int successiveCandles) : base(successiveCandles)
        {
        }

        public override bool IsMet()
        {
            if (IsExpired)
                return false;

            throw new NotImplementedException();
        }
    }
}