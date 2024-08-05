using CsvHelper.Configuration.Attributes;

namespace Gradient.CryptoAnalysis.OtherData
{
    public class Ema20_50_100_200 : IIndicator
    {
        [Name("Plot")]
        public double Plot100 { get; set; }

        [Name("Plot")]
        public double Plot20 { get; set; }

        [Name("Plot")]
        public double Plot200 { get; set; }

        [Name("Plot")]
        public double Plot50 { get; set; }
    }
}