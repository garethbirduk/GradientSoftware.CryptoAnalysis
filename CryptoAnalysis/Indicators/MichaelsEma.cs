using CsvHelper.Configuration.Attributes;

namespace Gradient.CryptoAnalysis.OtherData
{
    public class MichaelsEma : IIndicator
    {
        [Name("EMA Big")]
        public double EMABig { get; set; }

        [Name("EMA Small")]
        public double EMASmall { get; set; }
    }
}