namespace Gradient.CryptoAnalysis
{
    public class IsBreakOfStructureCondition : PriceCondition
    {
        public IsBreakOfStructureCondition(int successiveCandles) : base(successiveCandles)
        {
        }

        public override bool IsMet()
        {
            if (IsExpired)
                return false;

            var data = Prices.CreateSubsetByCount(SuccessiveCandles - 1, Price, true);
            var swings = data.ToSwings();
            var swing = data.ToSwings().Last();
            return swing.ConfirmedMarketStructureBreak == Price;
        }
    }
}