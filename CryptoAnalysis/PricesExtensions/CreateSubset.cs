namespace Gradient.CryptoAnalysis
{
    public static partial class PricesExtensions_CreateSubset
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
    }
}