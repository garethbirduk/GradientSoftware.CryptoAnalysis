namespace Gradient.CryptoAnalysis.Conditions.RangeConditions
{
    public class IsUptrendCondition : IsRangeCondition
    {
        public IsUptrendCondition(int successiveCandles) : base(successiveCandles)
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