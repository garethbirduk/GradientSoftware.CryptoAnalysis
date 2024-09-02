namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsWhiteBeltSystemCondition : PriceCondition
    {
        private ConditionSet ConditionSet { get; set; }
        private IsInRangeCondition IsInRangeCondition { get; set; }

        private IsSuccessiveUpswingsWithMaxMarketStructureBreaksCondition IsInUptrendCondition { get; set; }

        protected override bool IsMet()
        {
            IsInUptrendCondition.SetPrices(Prices, Cursor.None);
            IsInUptrendCondition.SetPrice(Price.DateTime);

            if (IsInUptrendCondition.MetAt == null && IsInUptrendCondition.IsMet(false))
            {
                IsInUptrendCondition.MetAt = Price;
            }

            IsInRangeCondition.SetPrices(Prices, Cursor.Last);
            IsInRangeCondition.SetPrice(Price.DateTime);

            return IsInUptrendCondition.MetAt != null && IsInRangeCondition.IsMet(false);
        }

        public IsWhiteBeltSystemCondition(int maxSwingSize, int minimumUpswings, int maxMarketStructureBreaks, int successiveCandles = DefaultAdditionalCandles) : base(successiveCandles)
        {
            IsInUptrendCondition = new IsSuccessiveUpswingsWithMaxMarketStructureBreaksCondition(maxSwingSize, minimumUpswings, maxMarketStructureBreaks, AdditionalCandles);
            IsInRangeCondition = new IsInRangeCondition();
        }
    }
}