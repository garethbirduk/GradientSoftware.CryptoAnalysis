namespace Gradient.CryptoAnalysis
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

            var data = Prices.TakePreviousPrices(SuccessiveCandles, CurrentIndex);

            if (data.Count() < MinDataSize)
                return false;

            return data.All(x => x.Close > x.Open);
        }
    }
}