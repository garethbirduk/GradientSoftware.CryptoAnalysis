namespace Gradient.CryptoAnalysis
{
    public static partial class PricesExtensions_Annotations
    {
        public static List<AnnotatedPrice> ToAnnotatedPrices(this IEnumerable<Price> prices, EnumAnnotationType type)
        {
            return prices.Select(p => new AnnotatedPrice
            {
                Close = p.Close,
                Open = p.Open,
                High = p.High,
                Low = p.Low,
                DateTime = p.DateTime,
                Indicators = p.Indicators,
                Annotations = new List<Annotation> { Annotation.Create(type) }
            }).ToList();
        }
    }
}