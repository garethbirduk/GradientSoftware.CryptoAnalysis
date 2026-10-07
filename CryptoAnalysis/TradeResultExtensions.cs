using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gradient.CryptoAnalysis
{
    public static class TradeResultExtensions
    {
        public static void SaveToJson(this IEnumerable<TradeResult> results, string filePath)
        {
            var folder = Path.GetDirectoryName(filePath);
            if (!Path.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters =
            {
                new JsonStringEnumConverter()
            }
            };

            string jsonString = JsonSerializer.Serialize(results, options);
            File.WriteAllText(filePath, jsonString);
        }
    }
}