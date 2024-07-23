namespace Gradient.CryptoAnalysis
{
    public class IsPriceIncreaseRateCondition : PriceCondition
    {
        public IsPriceIncreaseRateCondition(double percentageIncrease, int successiveCandles) : base(successiveCandles)
        {
            PercentageIncrease = percentageIncrease;
        }

        public double PercentageIncrease { get; set; }

        public override bool IsMet()
        {
            if (IsExpired)
                return false;

            var data = Prices.TakePreviousPrices(SuccessiveCandles, CurrentIndex);

            return data.HasIncreasedByPercentage(PercentageIncrease);
        }
    }
}