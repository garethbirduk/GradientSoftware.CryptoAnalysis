namespace Gradient.CryptoAnalysis
{
    public static partial class PricesExtensions_AllTimeLows
    {
        public static List<Price> AllTimeLows(this IEnumerable<Price> prices, EnumCloseType closeType)
        {
            if (!prices.Any())
                return new List<Price>();

            var list = new List<Price>
            {
                prices.First(),
            };

            foreach (var price in prices.Where(x => x != null))
            {
                if (price.CloseValue(closeType) < list.Last().CloseValue(closeType))
                    list.Add(price);
            }
            return list;
        }
    }
}