using Microsoft.IdentityModel.Tokens;

namespace Gradient.CryptoAnalysis
{
    public static class PricesExtensions
    {
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

        public static double PercentageChangeCloseToClose(this IEnumerable<Price> data)
        {
            var initialOpen = data.First().Close;
            var finalClose = data.Last().Close;

            return ((finalClose - initialOpen) / initialOpen) * 100;
        }

        public static List<List<Price>> ToSegments(this List<Price> prices)
        {
            if (!prices.Any())
                return new List<List<Price>>();

            var highs = prices.HighCloses();

            var segments = new List<List<Price>>();

            for (int i = 0; i < highs.Count; i++)
            {
                var high = highs[i];
                var startIndex = prices.IndexOf(high);

                var endIndex = prices.IndexOf(prices.Last()) + 1;
                if (high != highs.Last())
                {
                    endIndex = prices.IndexOf(highs[i + 1]);
                }

                var skip = startIndex;
                var take = endIndex - startIndex;

                var segment = prices.Skip(skip).Take(take).ToList();
                if (segment.Count() > 1)
                    segments.Add(segment);
            }

            return segments;
        }

        public static List<Upswing> ToSwings(this List<Price> prices)
        {
            var swings = new List<Upswing>();
            if (prices.Count() == 0)
                return swings;

            var segments = prices.ToSegments();
            foreach (var segment in segments.Where(x => x.Count() > 1))
            {
                Price? next = null;
                if (segment.Last() != prices.Last())
                    next = prices[prices.IndexOf(segment.Last()) + 1];

                var swing = new Upswing(segment, swings.LastOrDefault(), next);
                swings.Add(swing);
            }

            return swings;
        }
    }
}