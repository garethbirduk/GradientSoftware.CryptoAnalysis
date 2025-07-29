namespace Gradient.CryptoAnalysis
{
    public static partial class PricesExtensions_AllTimeHighs
    {
        public static List<Price> AllTimeHighs(this IEnumerable<Price> prices, EnumCloseType closeType)
        {
            if (!prices.Any())
                return new List<Price>();

            var list = new List<Price>
            {
                prices.First(),
            };

            foreach (var price in prices.Where(x => x != null))
            {
                if (price.CloseValue(closeType) > list.Last().CloseValue(closeType))
                    list.Add(price);
            }
            return list;
        }
    }
}