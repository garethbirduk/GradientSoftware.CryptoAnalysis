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

        public static List<Price> CreateSubset(this List<Price> prices, Price from, Price to, bool inclusive = false)
        {
            var fromIndex = prices.IndexOf(from);
            var toIndex = prices.IndexOf(to);
            return CreateSubsetByIndex(prices, fromIndex, toIndex, inclusive);
        }

        public static List<Price> CreateSubsetByCount(this List<Price> prices, Price from, int count, bool inclusive = false)
        {
            var fromIndex = prices.IndexOf(from);
            var toIndex = fromIndex + count;
            return CreateSubsetByIndex(prices, fromIndex, toIndex, inclusive);
        }

        public static List<Price> CreateSubsetByCount(this List<Price> prices, int count, Price to, bool inclusive = false)
        {
            var toIndex = prices.IndexOf(to);
            var fromIndex = toIndex - count;
            return CreateSubsetByIndex(prices, fromIndex, toIndex, inclusive);
        }

        public static List<Price> CreateSubsetByIndex(this List<Price> prices, Price from, int toIndex, bool inclusive = false)
        {
            var fromIndex = prices.IndexOf(from);
            return CreateSubsetByIndex(prices, fromIndex, toIndex, inclusive);
        }

        public static List<Price> CreateSubsetByIndex(this List<Price> prices, int fromIndex, Price to, bool inclusive = false)
        {
            var toIndex = prices.IndexOf(to);
            return CreateSubsetByIndex(prices, fromIndex, toIndex, inclusive);
        }

        public static List<Price> CreateSubsetByIndex(this List<Price> prices, int fromIndex, int toIndex, bool inclusive = false)
        {
            if (!inclusive)
                toIndex = toIndex - 1;
            if (fromIndex < 0)
                fromIndex = 0;
            return prices.Skip(fromIndex).Take(toIndex - fromIndex + 1).ToList();
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

                if (i == highs.Count - 1 && endIndex < prices.Count())
                {
                    // add any prices at the end that are not in a swing yet
                    list.Add(prices.Skip(endIndex + 1).ToList());
                }
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