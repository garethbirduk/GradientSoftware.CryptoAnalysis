using CryptoAnalysis;
using Microsoft.IdentityModel.Tokens;

namespace Gradient.CryptoAnalysis
{
    public static partial class PricesExtensions_Closes2
    {
        private static List<Price> ToHighHighsUsingCloses(this IEnumerable<Price> prices)
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

        private static List<Price> ToHighHighsUsingHighs(this IEnumerable<Price> prices)
        {
            if (!prices.Any())
                return new List<Price>();

            var list = new List<Price>
            {
                prices.First(),
            };

            foreach (var price in prices.Where(x => x != null))
            {
                if (price.IsGreen() && price.High > list.Last().High)
                    list.Add(price);
            }

            return list;
        }

        private static List<Price> ToLowHighsUsingCloses(this IEnumerable<Price> prices)
        {
            if (!prices.Any())
                return new List<Price>();

            var list = new List<Price>
            {
                prices.First(),
            };

            foreach (var price in prices.Where(x => x != null))
            {
                if (price.Close < list.Last().Close)
                    list.Add(price);
            }

            return list;
        }

        private static List<Price> ToLowHighsUsingHighs(this IEnumerable<Price> prices)
        {
            if (!prices.Any())
                return new List<Price>();

            var list = new List<Price>
            {
                prices.First(),
            };

            foreach (var price in prices.Where(x => x != null))
            {
                if (price.IsRed() && price.High < list.Last().High)
                    list.Add(price);
            }

            return list;
        }

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

        public static List<Price> HighClosesIsGreen(this IEnumerable<Price> prices, EnumCloseType closeType)
        {
            if (!prices.Any())
                return new List<Price>();

            var list = new List<Price>
            {
                prices.First(),
            };

            foreach (var price in prices.Where(x => x != null))
            {
                if (price.IsGreen())
                {
                    if (price.CloseValue(closeType) > list.Last().CloseValue(closeType))
                        list.Add(price);
                }
            }

            return list;
        }

        public static List<Price> HighLows(this IEnumerable<Price> prices)
        {
            if (!prices.Any())
                return new List<Price>();

            var list = new List<Price>
            {
                prices.First(),
            };

            foreach (var price in prices.Where(x => x != null))
            {
                if (price.IsRed() && price.High > list.Last().High)
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
                if (price.IsRed() && price.Close < list.Last().Close)
                    list.Add(price);
            }

            return list;
        }

        public static List<Price> LowClosesIsRed(this IEnumerable<Price> prices, EnumCloseType closeType)
        {
            if (!prices.Any())
                return new List<Price>();

            var list = new List<Price>
            {
                prices.First(),
            };

            foreach (var price in prices.Where(x => x != null))
            {
                if (price.IsRed())
                {
                    if (price.CloseValue(closeType) < list.Last().CloseValue(closeType))
                        list.Add(price);
                }
            }

            return list;
        }

        public static List<Price> LowLows(this IEnumerable<Price> prices)
        {
            if (!prices.Any())
                return new List<Price>();

            var list = new List<Price>
            {
                prices.First(),
            };

            foreach (var price in prices.Where(x => x != null))
            {
                if (price.IsRed() && price.Low < list.Last().Low)
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

        public static List<Price> ToHigherLows(this IEnumerable<Price> prices, EnumCloseType closeType)
        {
            switch (closeType)
            {
                default: case EnumCloseType.Close: return prices.ToLowHighsUsingCloses();
                case EnumCloseType.High: return prices.ToHighHighsUsingHighs();
            }
        }

        public static List<Price> ToHighHighs(this IEnumerable<Price> prices, EnumCloseType closeType)
        {
            switch (closeType)
            {
                default: case EnumCloseType.Close: return prices.ToHighHighsUsingCloses();
                case EnumCloseType.High: return prices.ToHighHighsUsingHighs();
            }
        }
    }
}