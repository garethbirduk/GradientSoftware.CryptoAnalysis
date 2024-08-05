using CsvHelper.Configuration.Attributes;

namespace Gradient.CryptoAnalysis
{
    public class Price
    {
        [Name("close")]
        public double Close { get; set; }

        [Name("time")]
        public DateTime DateTime { get; set; }

        [Name("high")]
        public double High { get; set; }

        [Ignore]
        public Gradient.CryptoAnalysis.OtherData.Indicators Indicators { get; set; } = new();

        [Name("low")]
        public double Low { get; set; }

        [Name("open")]
        public double Open { get; set; }
    }
}