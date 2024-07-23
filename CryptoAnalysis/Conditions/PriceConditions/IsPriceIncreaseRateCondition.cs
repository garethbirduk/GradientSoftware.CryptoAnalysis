namespace Gradient.CryptoAnalysis
{
    public class IsPriceIncreaseRateCondition : PriceCondition
    {
        public IsPriceIncreaseRateCondition(double percentageIncrease, int successiveCandles, bool max = false) : base(successiveCandles)
        {
            PercentageIncrease = percentageIncrease;
            PeriodsToTakeIsMaxPeriodsNotAbsolute = max;
        }

        public double PercentageIncrease { get; set; }
        public bool PeriodsToTakeIsMaxPeriodsNotAbsolute { get; }

        public override bool IsMet()
        {
            if (IsExpired)
                return false;

            var periods = new List<int>() { SuccessiveCandles };
            if (PeriodsToTakeIsMaxPeriodsNotAbsolute)
            {
                periods = Enumerable.Range(1, SuccessiveCandles).Reverse().ToList();
            }

            foreach (var period in periods)
            {
                var data = Prices.TakePreviousPrices(period, CurrentIndex);
                if (data.HasIncreasedByPercentage(PercentageIncrease))
                    return true;
            }

            return false;
        }
    }
}