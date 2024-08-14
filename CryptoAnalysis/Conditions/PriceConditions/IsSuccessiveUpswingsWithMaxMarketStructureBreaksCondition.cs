namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsSuccessiveUpswingsWithMaxMarketStructureBreaksCondition : PriceCondition, IAdjustableCandles
    {
        protected override bool IsMet()
        {
            var data = Prices.CreateSubsetByCount(SuccessiveCandles - 1, Price, true);

            var swings = data.ToUpswings();
            if (swings.Count < MinimumUpswings)
                return false;
            swings = swings.TakeLast(MinimumUpswings).ToList();

            if (swings.Count(x => x.MarketStructureBreak != null) > MaxMarketStructureBreaks)
                return false;

            return true;
        }

        public IsSuccessiveUpswingsWithMaxMarketStructureBreaksCondition(int successiveCandles, int minimumUpswings, int maxMarketStructureBreaks) : base(successiveCandles)
        {
            MinimumUpswings = minimumUpswings;
            MaxMarketStructureBreaks = maxMarketStructureBreaks;
        }

        public int MaxMarketStructureBreaks { get; }
        public int MinimumUpswings { get; }

        public void SetSuccessiveCandles(int successiveCandles)
        {
            SuccessiveCandles = successiveCandles;
        }
    }
}