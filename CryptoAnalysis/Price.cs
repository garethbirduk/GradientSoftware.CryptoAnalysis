using CsvHelper.Configuration.Attributes;

namespace Gradient.CryptoAnalysis
{
    public interface IIndicator
    {
    }

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

    public class Indicators
    {
        public Ema20_50_100_200? Ema20_50_100_200 { get; set; }
        public MichaelsEma? MichaelsEma { get; set; }
    }

    public class MichaelsEma : IIndicator
    {
        [Name("EMA Big")]
        public double EMABig { get; set; }

        [Name("EMA Small")]
        public double EMASmall { get; set; }
    }

    public class Price
    {
        [Name("close")]
        public double Close { get; set; }

        [Name("high")]
        public double High { get; set; }

        public Indicators Indicators { get; set; } = new();

        [Name("low")]
        public double Low { get; set; }

        [Name("open")]
        public double Open { get; set; }

        [Name("time")]
        public DateTime DateTime { get; set; }
    }
}