using CsvHelper.Configuration.Attributes;

namespace Gradient.CryptoAnalysis
{
    public static class PriceExtensions
    {
        public static bool IsGreen(this Price price)
        {
            return price.Close > price.Open;
        }

        public static bool IsRed(this Price price)
        {
            return price.Close < price.Open;
        }
    }

    public class Price
    {
        [Name("close")]
        public double Close { get; set; }

        [Name("time")]
        public DateTime DateTime { get; set; }

        [Name("high")]
        public double High { get; set; }

        public Gradient.CryptoAnalysis.OtherData.Indicators Indicators { get; set; } = new();

        [Name("low")]
        public double Low { get; set; }

        [Name("open")]
        public double Open { get; set; }

        public override string ToString()
        {
            return $"{DateTime} : {Close}";
        }
    }
}