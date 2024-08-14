namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public interface IAdjustableCandles
    {
        public void SetSuccessiveCandles(int successiveCandles);
    }

    public class IsBreakOfStructureCondition : PriceCondition, IAdjustableCandles
    {
        public IsBreakOfStructureCondition(int successiveCandles = MaxSuccessiveCandles) : base(successiveCandles)
        {
        }

        public override bool IsMet()
        {
            if (IsExpired)
                return false;

            var data = Prices.CreateSubsetByCount(SuccessiveCandles - 1, Price, true);
            var swings = data.ToSwings();
            var swing = swings.LastOrDefault();
            if (swing == null)
                return false;
            var result = swing.BreakOfStructure != null && swing.BreakOfStructure == Price;
            return result;
        }

        public void SetSuccessiveCandles(int successiveCandles)
        {
            SuccessiveCandles = successiveCandles;
        }
    }
}