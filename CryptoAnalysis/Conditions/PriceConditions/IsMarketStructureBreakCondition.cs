namespace Gradient.CryptoAnalysis
{
    public class IsMarketStructureBreakCondition : PriceCondition, IAdjustableCandles
    {
        public IsMarketStructureBreakCondition(int successiveCandles = MaxSuccessiveCandles) : base(successiveCandles)
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
            var result = swing.MarketStructureBreak != null && swing.MarketStructureBreak == Price;
            return result;
        }

        public void SetSuccessiveCandles(int successiveCandles)
        {
            SuccessiveCandles = successiveCandles;
        }
    }

    public class IsSuccessiveUpswingsWithMaxMarketStructureBreaksCondition : PriceCondition, IAdjustableCandles
    {
        public IsSuccessiveUpswingsWithMaxMarketStructureBreaksCondition(int successiveCandles, int minimumUpswings, int maxMarketStructureBreaks) : base(successiveCandles)
        {
            MinimumUpswings = minimumUpswings;
            MaxMarketStructureBreaks = maxMarketStructureBreaks;
        }

        public int MaxMarketStructureBreaks { get; }
        public int MinimumUpswings { get; }

        public override bool IsMet()
        {
            if (IsExpired)
                return false;

            var data = Prices.CreateSubsetByCount(SuccessiveCandles - 1, Price, true);

            var swings = data.ToSwings();
            if (swings.Count < MinimumUpswings)
                return false;
            swings = swings.TakeLast(MinimumUpswings).ToList();

            if (swings.Count(x => x.MarketStructureBreak != null) > MaxMarketStructureBreaks)
                return false;

            return true;
        }

        public void SetSuccessiveCandles(int successiveCandles)
        {
            SuccessiveCandles = successiveCandles;
        }
    }
}