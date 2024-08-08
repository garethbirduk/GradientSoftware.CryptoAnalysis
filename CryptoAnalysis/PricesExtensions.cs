using Microsoft.IdentityModel.Tokens;

namespace Gradient.CryptoAnalysis
{
    public static class PricesExtensions
    {
        internal static double PercentageChangeCloseToClose(this IEnumerable<Price> data)
        {
            var initialOpen = data.First().Close;
            var finalClose = data.Last().Close;

            return ((finalClose - initialOpen) / initialOpen) * 100;
        }

        public static List<List<Price>> GetSegments(this List<Price> prices)
        {
            if (prices == null || !prices.Any())
                return new List<List<Price>>();

            var highs = prices.HighCloses();

            var list = new List<List<Price>>();

            for (int i = 0; i < highs.Count; i++)
            {
                var high = highs[i];
                var startIndex = prices.IndexOf(high);
                var endIndex = startIndex;
                if (i < highs.Count - 1)
                {
                    endIndex = prices.IndexOf(highs[i + 1]);
                }
                var segment = prices.Skip(startIndex).Take(endIndex - startIndex).ToList();
                list.Add(segment);
            }

            return list;
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

        public static List<Price> HighCloses(this IEnumerable<Price> prices)
        {
            if (prices == null || !prices.Any())
                return new List<Price>();

            var list = new List<Price>
            {
                prices.First(),
            };

            foreach (var price in prices)
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

        public static List<Swing> ToSwings(this List<Price> prices)
        {
            var list = new List<Swing>();
            if (prices.Count() == 0)
                return list;

            var allSegments = prices.GetSegments();
            var segments = allSegments.ConcatenateSegments();
            foreach (var segmentPrices in segments)
            {
                Price? next = null;
                if (segmentPrices != segments.Last())
                {
                    var index = segments.IndexOf(segmentPrices);
                    next = segments[index + 1].First();
                }
                list.Add(new Swing(segmentPrices, list.LastOrDefault(), next));
            }

            return list;
        }
    }
}