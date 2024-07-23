using PostSharp.Patterns.Contracts;

namespace Gradient.CryptoAnalysis
{
    public class PriceIncreaseRate : EnterTradeCondition
    {
        public PriceIncreaseRate([Required] List<Price> data, double percentageIncrease, int timePeriods)
        {
            Data = data;
            PercentageIncrease = percentageIncrease;
            TimePeriods = timePeriods;
        }

        public double PercentageIncrease { get; }
        public int TimePeriods { get; }

        public override bool IsMet(int index)
        {
            if (Data.Count < index)
                return false;
            if (Data.Count < TimePeriods)
                return false;
            if (Data.Count < index + TimePeriods)
                return false;
            return Data.HasIncreasedByPercentage(PercentageIncrease, index - TimePeriods, TimePeriods);
        }
    }
}