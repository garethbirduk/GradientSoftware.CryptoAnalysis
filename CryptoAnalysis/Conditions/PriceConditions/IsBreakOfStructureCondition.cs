namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public interface IAdjustableCandles
    {
        public void SetSuccessiveCandles(int additionalCandles);
    }

    public class IsBreakOfStructureCondition : PriceCondition, IAdjustableCandles
    {
        protected override bool IsMet()
        {
            var data = Prices.CreateSubsetByCount(AdditionalCandles - 1, Price, true);
            var swings = data.ToUpswings();
            var swing = swings.LastOrDefault();
            if (swing == null)
                return false;
            var result = swing.BreakOfStructure != null && swing.BreakOfStructure == Price;
            return result;
        }

        public IsBreakOfStructureCondition(int additionalCandles = DefaultAdditionalCandles) : base(additionalCandles)
        {
        }

        public void SetSuccessiveCandles(int additionalCandles)
        {
            AdditionalCandles = additionalCandles;
        }
    }
}