namespace Gradient.CryptoAnalysis.Conditions.RangeConditions
{
    public class IsUptrendCondition : IsRangeCondition
    {
        protected override bool IsMet()
        {
            throw new NotImplementedException();
        }

        public IsUptrendCondition(int successiveCandles) : base(successiveCandles)
        {
        }
    }
}