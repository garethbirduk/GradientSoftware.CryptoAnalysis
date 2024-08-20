namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsSuccessiveUpswingsWithMaxMarketStructureBreaksCondition : PriceCondition, IAdjustableCandles
    {
        protected override bool IsMet()
        {
            var data = Prices.CreateSubsetByCount(SuccessiveCandles - 1, Price, true);

            var swings = data.ToUpswings(MaxSwingSize).ToList();

            if (swings.Count < MinimumUpswings)
                return false;
            var lastUpswings = swings.TakeLast(MinimumUpswings).ToList();

            if (lastUpswings.Count(x => x.MarketStructureBreak != null) > MaxMarketStructureBreaks)
                return false;

            return true;
        }

        public IsSuccessiveUpswingsWithMaxMarketStructureBreaksCondition(int maxSwingSize, int minimumUpswings, int maxMarketStructureBreaks, int successiveCandles = DefaultSuccessiveCandles) : base(successiveCandles)
        {
            MaxSwingSize = maxSwingSize;
            MinimumUpswings = minimumUpswings;
            MaxMarketStructureBreaks = maxMarketStructureBreaks;
        }

        public int MaxMarketStructureBreaks { get; }
        public int MaxSwingSize { get; }
        public Price? MetAt { get; set; }
        public int MinimumUpswings { get; }

        public void SetSuccessiveCandles(int successiveCandles)
        {
            SuccessiveCandles = successiveCandles;
        }
    }
}