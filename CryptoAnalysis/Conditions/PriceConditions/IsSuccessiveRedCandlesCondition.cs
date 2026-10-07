namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsSuccessiveRedCandlesCondition : PriceCondition
    {
        protected override bool IsMet()
        {
            var data = Prices.CreateSubsetByCount(AdditionalCandles - 1, Price, true);

            if (data.Count() < MinDataSize)
                return true;

            if (data.Count() < AdditionalCandles)
                return false;

            return data.All(x => x.Close < x.Open);
        }

        public IsSuccessiveRedCandlesCondition(int successiveCandles = DefaultAdditionalCandles) : base(successiveCandles)
        {
            MinDataSize = 1;
        }
    }
}