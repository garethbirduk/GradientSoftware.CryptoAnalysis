namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsInRangeCondition : PriceCondition
    {
        private int MaxSwingSize;

        protected override bool IsMet()
        {
            var swings = Prices.ToUpswings(EnumCloseType.Close, MaxSwingSize).ToList();
            if (swings.Count < 2)
                return false;

            var swing = swings[swings.Count - 1];
            var previous = swings[swings.Count - 2];

            var prices = previous.Prices.Skip(previous.Prices.IndexOf(previous.SwingLow))
                .Union(swing.Prices).ToList();

            var c = new IsPriceDecreaseRateCondition(25, AdditionalCandles, SubsetType.LowestToLast);
            c.SetPrices(prices.ToList(), Cursor.Last);
            var result = c.IsMet(false);

            return result;
        }

        public IsInRangeCondition(int maxSwingSize, int additionalCandles = DefaultAdditionalCandles) : base(additionalCandles)
        {
            MaxSwingSize = maxSwingSize;
        }
    }
}