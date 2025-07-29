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
                switch (closeType)
                {
                    case EnumCloseType.Close:
                        {
                            if (price.Close > list.Last().Close)
                                list.Add(price);
                            break;
                        }
                    case EnumCloseType.High:
                        {
                            if (price.High > list.Last().High)
                                list.Add(price);
                            break;
                        }
                    default:
                        throw new NotSupportedException("EnumCloseType must be specified");
                }
            }
            return list;
        }
    }
}