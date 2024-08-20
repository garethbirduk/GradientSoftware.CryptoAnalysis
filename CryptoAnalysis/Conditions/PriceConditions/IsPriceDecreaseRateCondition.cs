namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public class IsPriceDecreaseRateCondition : PriceCondition
    {
        protected override bool IsMet()
        {
            try
            {
                var periods = new List<int>() { SuccessiveCandles };
                if (PeriodsToTakeIsMaxPeriodsNotAbsolute)
                {
                    periods = periods.TakeLast(SuccessiveCandles).ToList();
                }

                foreach (var period in periods)
                {
                    var data = Prices.CreateSubsetByCount(period - 1, Price, true);
                    if (data.HasDecreasedByPercentage(PercentageDecrease))
                        return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public IsPriceDecreaseRateCondition(double percentageDecrease, int successiveCandles, bool max = false) : base(successiveCandles)
        {
            PercentageDecrease = percentageDecrease;
            PeriodsToTakeIsMaxPeriodsNotAbsolute = max;
        }

        public double PercentageDecrease { get; set; }
        public bool PeriodsToTakeIsMaxPeriodsNotAbsolute { get; }
    }
}