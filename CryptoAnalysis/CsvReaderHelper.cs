using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Globalization;

namespace Gradient.CryptoAnalysis
{
    public class BacktestResultMap : ClassMap<BacktestResult>
    {
        public BacktestResultMap()
        {
            Map(m => m.Coin).Name("Coin");
            Map(m => m.EntryDateTime).Name("Entry Date & Time");
            Map(m => m.ExitDateTime).Name("Exit Date & Time");
            Map(m => m.Direction).Name("Direction");
            Map(m => m.Entry).Name("Entry");
            Map(m => m.StopLoss).Name("Stop Loss");
            Map(m => m.Exit).Name("Exit");
            Map(m => m.Returns).Name("Returns");
            Map(m => m.WinLoss).Name("Win / Loss");
        }
    }

    public class CsvReaderHelper
    {
        public IEnumerable<BacktestResult> ReadBacktestResults(string filePath)
        {
            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Context.RegisterClassMap<BacktestResultMap>();
                var records = csv.GetRecords<BacktestResult>();
                return new List<BacktestResult>(records);
            }
        }

        public IEnumerable<TClass> ReadData<TClass, TClassMap>(string filePath)
            where TClass : class
            where TClassMap : ClassMap
        {
            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Context.RegisterClassMap<TClassMap>();
                var records = csv.GetRecords<TClass>();
                return new List<TClass>(records);
            }
        }

        public void WriteBacktestResults(string filepath, IEnumerable<BacktestResult> results)
        {
            WriteData<BacktestResult, BacktestResultMap>(filepath, results);
        }

        public void WriteData<TClass, TClassMap>(string filePath, IEnumerable<TClass> data)
            where TClass : class
            where TClassMap : ClassMap
        {
            using (var writer = new StreamWriter(filePath))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.Context.RegisterClassMap<TClassMap>();
                csv.WriteRecords(data);
            }
        }
    }

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

    public class PriceClassMap : ClassMap<Price>
    {
        public PriceClassMap()
        {
            AutoMap(CultureInfo.InvariantCulture);
            Map(m => m.Close).Name("close").NameIndex(0).Validate(args => !string.IsNullOrEmpty(args.Field));
            Map(m => m.High).Name("high").NameIndex(0).Validate(args => !string.IsNullOrEmpty(args.Field));
            Map(m => m.Low).Name("low").NameIndex(0).Validate(args => !string.IsNullOrEmpty(args.Field));
            Map(m => m.Open).Name("open").NameIndex(0).Validate(args => !string.IsNullOrEmpty(args.Field));
            Map(m => m.DateTime).Name("time").NameIndex(0).Validate(args => !string.IsNullOrEmpty(args.Field));
            Map(m => m.Indicators).TypeConverter<IndicatorsConverter>().Optional();
        }
    }
}