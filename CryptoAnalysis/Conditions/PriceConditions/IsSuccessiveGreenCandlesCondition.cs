namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsSuccessiveGreenCandlesCondition : PriceCondition
    {
        public IsSuccessiveGreenCandlesCondition(int successiveCandles) : base(successiveCandles)
        {
        }

        public override bool IsMet()
        {
            if (IsExpired)
                return false;

            if (SuccessiveCandles < 2)
                return false;

            var data = Prices.CreateSubsetByCount(SuccessiveCandles - 1, Price, true);

            if (data.Count() < MinDataSize)
                return false;

            if (data.Count() < SuccessiveCandles)
                return false;

            return data.All(x => x.Close > x.Open);
        }
    }
}