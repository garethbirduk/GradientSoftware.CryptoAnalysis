namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
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

            try
            {
                var periods = new List<int>() { SuccessiveCandles };
                if (PeriodsToTakeIsMaxPeriodsNotAbsolute)
                {
                    periods = Enumerable.Range(1, SuccessiveCandles).Reverse().ToList();
                }

                foreach (var period in periods)
                {
                    var data = Prices.CreateSubsetByCount(period - 1, Price, true);
                    if (data.HasIncreasedByPercentage(PercentageIncrease))
                        return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}