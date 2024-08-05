﻿namespace Gradient.CryptoAnalysis
{
    public static class PriceExtensions
    {
        internal static double PercentageChangeCloseToClose(this IEnumerable<Price> data)
        {
            var initialOpen = data.First().Close;
            var finalClose = data.Last().Close;

            return ((finalClose - initialOpen) / initialOpen) * 100;
        }

        public static bool HasDecreasedByPercentage(this IEnumerable<Price> data, double percentageIncrease)
        {
            return HasIncreasedByPercentage(data, -percentageIncrease);
        }

        public static bool HasIncreasedByPercentage(this IEnumerable<Price> data, double percentageIncrease)
        {
            var change = PercentageChangeCloseToClose(data);

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
                return new List<Price>();

            int start = currentIndex - periods + 1;
            return prices.Skip(start).Take(periods);
        }
    }
}