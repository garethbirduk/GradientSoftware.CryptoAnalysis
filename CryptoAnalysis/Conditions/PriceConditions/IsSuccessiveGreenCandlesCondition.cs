namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsSuccessiveGreenCandlesCondition : PriceCondition
    {
        protected override bool IsMet()
        {
            if (SuccessiveCandles < 2)
                return false;

            var data = Prices.CreateSubsetByCount(SuccessiveCandles - 1, Price, true);

            if (data.Count() < MinDataSize)
                return false;

            if (data.Count() < SuccessiveCandles)
                return false;

            return data.All(x => x.Close > x.Open);
        }

        public IsSuccessiveGreenCandlesCondition(int successiveCandles = DefaultSuccessiveCandles) : base(successiveCandles)
        {
        }
    }
}