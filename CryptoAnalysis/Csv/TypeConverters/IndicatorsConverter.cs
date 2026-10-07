using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace Gradient.CryptoAnalysis.Csv
{
    public class IndicatorsConverter : DefaultTypeConverter
    {
        public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
        {
            var indicators = new Indicators();

            if (row.TryGetField<double>("EMA Big", out var emaBig) && row.TryGetField<double>("EMA Small", out var emaSmall))
            {
                indicators.MichaelsEma = new MichaelsEma
                {
                    EMABig = emaBig,
                    EMASmall = emaSmall
                };
            }

            if (row.TryGetField<double>("Plot100", out var plot100) && row.TryGetField<double>("Plot20", out var plot20) &&
                row.TryGetField<double>("Plot200", out var plot200) && row.TryGetField<double>("Plot50", out var plot50))
            {
                indicators.Ema20_50_100_200 = new Ema20_50_100_200
                {
                    Plot100 = plot100,
                    Plot20 = plot20,
                    Plot200 = plot200,
                    Plot50 = plot50
                };
            }

            return indicators;
        }
    }
}