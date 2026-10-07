using CryptoAnalysis.Csv.ClassMaps;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace Gradient.CryptoAnalysis.Csv
{
    public class CsvReaderHelper
    {
        public IEnumerable<BacktestResult> ReadBacktestResults(string filePath)
        {
            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Context.RegisterClassMap<BacktestResultClassMap>();
                var records = csv.GetRecords<BacktestResult>();
                return new List<BacktestResult>(records);
            }
        }

        public IEnumerable<TClass> ReadData<TClass, TClassMap>(string filePath)
            where TClass : class
            where TClassMap : ClassMap
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null, // Ignore missing headers
                MissingFieldFound = null, // Ignore missing fields
            };

            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, config))
            {
                csv.Context.RegisterClassMap<TClassMap>();
                var records = csv.GetRecords<TClass>();
                return new List<TClass>(records);
            }
        }

        public void WriteBacktestResults(string filepath, IEnumerable<BacktestResult> results)
        {
            WriteData<BacktestResult, BacktestResultClassMap>(filepath, results);
        }

        public void WriteData<TClass, TClassMap>(string filePath, IEnumerable<TClass> data)
            where TClass : class
            where TClassMap : ClassMap
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            using (var writer = new StreamWriter(filePath))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.Context.RegisterClassMap<TClassMap>();
                csv.WriteRecords(data);
            }
        }
    }
}