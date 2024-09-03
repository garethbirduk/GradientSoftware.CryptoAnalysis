using CryptoAnalysis;
using Microsoft.IdentityModel.Tokens;

namespace Gradient.CryptoAnalysis
{
    public static partial class PricesExtensions_Closes
    {
        public static bool HasDecreasedByPercentage(this IEnumerable<Price> data, double percentageDecrease)
        {
            var change = -PercentageIncreaseCloseToClose(data);
            return change >= percentageDecrease;
        }

        public static bool HasIncreasedByPercentage(this IEnumerable<Price> data, double percentageIncrease)
        {
            var change = PercentageIncreaseCloseToClose(data);
            return change >= percentageIncrease;
        }

        public static List<Price> HighCloses(this IEnumerable<Price> prices)
        {
            if (!prices.Any())
                return new List<Price>();

            var list = new List<Price>
            {
                prices.First(),
            };

            foreach (var price in prices.Where(x => x != null))
            {
                if (price.Close > list.Last().Close)
                    list.Add(price);
            }

            return list;
        }

        public static List<Price> LowCloses(this IEnumerable<Price> values)
        {
            if (values.IsNullOrEmpty())
                return new List<Price>();

            var list = new List<Price>()
            {
                values.First(),
            };

            foreach (var price in values)
            {
                if (price.Close < list.Last().Close)
                    list.Add(price);
            }

            return list;
        }

        public static double PercentageIncreaseCloseToClose(this IEnumerable<Price> data)
        {
            var initialClose = data.First().Close;
            var finalClose = data.Last().Close;

            return Maths.PercentageIncrease(initialClose, finalClose);
        }
    }
}