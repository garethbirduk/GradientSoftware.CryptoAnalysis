using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis;

namespace Gradient.CryptoAnalysis.Csv
{
    public class CsvCombiner
    {
        private static List<Price> LoadCsvFiles(string filepath)
        {
            var records = new List<Price>();
            if (!File.Exists(filepath))
            {
                throw new FileNotFoundException("The specified input file does not exist.", filepath);
            }

            string? directory = Path.GetDirectoryName(filepath);
            if (directory == null || !Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException("The specified directory does not exist.");
            }

            string baseFilename = Path.GetFileNameWithoutExtension(filepath);
            string searchPattern = $"{baseFilename}*.csv";

            var files = Directory.GetFiles(directory, searchPattern);
            if (files.Length == 0)
            {
                throw new FileNotFoundException("No matching files found to combine.");
            }

            records = new List<Price>();
            var csvHelper = new CsvReaderHelper();
            foreach (var file in files)
            {
                var fileRecords = csvHelper.ReadData<Price, PriceClassMap>(file).ToList();
                records.AddRange(fileRecords);
            }
            return records;
        }

        private static void SaveCsvFile<T>(string outputFilePath, IEnumerable<T> records)
            where T : Price
        {
            var csvHelper = new CsvReaderHelper();
            try
            {
                csvHelper.WriteData<Price, PriceClassMap>(outputFilePath, records);
            }
            catch (IOException ex)
            {
                throw new IOException("The output file path is inaccessible. It might be open in another application.", ex);
            }
        }

        public void CombineCsvFiles(string filepath, string outputFilePath, Func<Price, DateTime> groupBy, Func<Price, DateTime> orderBy)
        {
            var records = LoadCsvFiles(filepath)
                .OrderBy(orderBy)
                .GroupBy(groupBy)
                .Select(x => x.First()).ToList();

            SaveCsvFile(outputFilePath, records);
        }
    }
}