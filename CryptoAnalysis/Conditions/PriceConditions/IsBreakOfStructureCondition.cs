namespace Gradient.CryptoAnalysis
{
    public interface IAdjustableCandles
    {
        public void SetSuccessiveCandles(int successiveCandles);
    }

    public class IsBreakOfStructureCondition : PriceCondition, IAdjustableCandles
    {
        public IsBreakOfStructureCondition(int successiveCandles) : base(successiveCandles)
        {
        }

        public override bool IsMet()
        {
            if (IsExpired)
                return false;

            var closes = Prices.CreateSubsetByIndex(CurrentIndex - 2, CurrentIndex + 2, true).Select(x => x.Close).ToList();

            var data = Prices.CreateSubsetByCount(SuccessiveCandles - 1, Price, true);
            var swings = data.ToSwings();
            var swing = swings.LastOrDefault();
            if (swing == null)
                return false;
            var result = swing.ConfirmedMarketStructureBreak != null && swing.ConfirmedMarketStructureBreak == Price;
            return result;
        }

        public void SetSuccessiveCandles(int successiveCandles)
        {
            SuccessiveCandles = successiveCandles;
        }
    }
}