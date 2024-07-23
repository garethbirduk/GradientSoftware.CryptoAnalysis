using CsvHelper;
using CsvHelper.Configuration;
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

    public class CryptoCoinDataMap : ClassMap<Price>
    {
        public CryptoCoinDataMap()
        {
            Map(m => m.Time).Name("time").TypeConverter<UtcDateTimeConverter>();
            Map(m => m.Open).Name("open");
            Map(m => m.High).Name("high");
            Map(m => m.Low).Name("low");
            Map(m => m.Close).Name("close");
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

        public IEnumerable<Price> ReadCryptoCoinData(string filePath)
        {
            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Context.RegisterClassMap<CryptoCoinDataMap>();
                var records = csv.GetRecords<Price>();
                return new List<Price>(records);
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
}