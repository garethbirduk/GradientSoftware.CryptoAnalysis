namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsInRangeCondition : PriceCondition
    {
        protected override bool IsMet()
        {
            var swings = Prices.ToUpswings(2).ToList();
            if (swings.Count < 2)
                return false;

            var swing = swings[swings.Count - 1];
            var previous = swings[swings.Count - 2];

            var prices = previous.Prices.Skip(previous.Prices.IndexOf(previous.SwingLow))
                .Union(swing.Prices);

            var c = new IsPriceDecreaseRateCondition(50, DefaultAdditionalCandles, SubsetType.LowestToLast);
            c.SetPrices(prices.ToList(), Cursor.Last);
            var result = c.IsMet(false);

            return result;
        }
    }
}