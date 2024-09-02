namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsMarketStructureBreakCondition : PriceCondition, IAdjustableCandles
    {
        protected override bool IsMet()
        {
            var data = Prices.CreateSubsetByCount(AdditionalCandles - 1, Price, true);
            var swings = data.ToUpswings();
            var swing = swings.LastOrDefault();
            if (swing == null)
                return false;
            var result = swing.MarketStructureBreak != null && swing.MarketStructureBreak == Price;
            return result;
        }

        public IsMarketStructureBreakCondition(int successiveCandles = DefaultAdditionalCandles) : base(successiveCandles)
        {
        }

        public void SetSuccessiveCandles(int successiveCandles)
        {
            AdditionalCandles = successiveCandles;
        }
    }
}