namespace Gradient.CryptoAnalysis
{
    public static class PriceExtensions
    {
        internal static double PercentageChange(this IEnumerable<Price> data, int skip = 0, int take = 0)
        {
            data = data.Skip(skip);
            if (take > 0)
                data = data.Take(take);

            var initialOpen = data.First().Open;
            var finalClose = data.Last().Close;

            return ((finalClose - initialOpen) / initialOpen) * 100;
        }

        public static bool HasIncreasedByPercentage(this IEnumerable<Price> data, double percentageIncrease, int skip = 0, int take = 0)
        {
            var change = PercentageChange(data, skip, take);

            if (change < 0 && percentageIncrease < 0)
                return change <= percentageIncrease;
            return change >= percentageIncrease;
        }

        public static IEnumerable<Price> TakePreviousPrices(this List<Price> prices, int periods, int currentIndex)
        {
            if (currentIndex < 0 || currentIndex >= prices.Count)
                throw new ArgumentOutOfRangeException(nameof(currentIndex), "Current index is out of range.");

            if (periods <= 0)
                throw new ArgumentOutOfRangeException(nameof(periods), "Periods must be greater than zero.");

            if (currentIndex - periods + 1 < 0)
                throw new ArgumentException("Not enough periods in the prices to take.");

            int start = currentIndex - periods + 1;
            return prices.Skip(start).Take(periods);
        }
    }

    public class StopLossCalculation
    {
        public List<Price> Data { get; set; }

        public double Calculate(int index)
        {
            return Data[index].Open / 2.0;
        }
    }

    public class TakeProfitCalculation
    {
        public List<Price> Data { get; set; }

        public double Calculate(int index)
        {
            var tp = Data[index].Open * 1.5;
            return tp;
        }
    }
}