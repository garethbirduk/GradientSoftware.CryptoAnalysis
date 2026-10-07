namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsSuccessiveGreenCandlesCondition : PriceCondition
    {
        protected override bool IsMet()
        {
            var data = Prices.CreateSubsetByCount(AdditionalCandles - 1, Price, true);

            if (data.Count() < MinDataSize)
                return true;

            if (data.Count() < AdditionalCandles)
                return false;

            return data.All(x => x.Close > x.Open);
        }

        public IsSuccessiveGreenCandlesCondition(int additionalCandles = DefaultAdditionalCandles) : base(additionalCandles)
        {
            MinDataSize = 1;
        }
    }
}