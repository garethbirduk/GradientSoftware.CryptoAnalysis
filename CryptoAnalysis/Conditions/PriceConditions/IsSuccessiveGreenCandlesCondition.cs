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

            try
            {
                var data = Prices.TakePreviousPrices(SuccessiveCandles, CurrentIndex);

                if (data.Count() < MinDataSize)
                    throw new NotSupportedException($"Prices Size of {Prices.Count} < Minimum Prices Size of {MinDataSize}");

                return data.All(x => x.Close > x.Open);
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}