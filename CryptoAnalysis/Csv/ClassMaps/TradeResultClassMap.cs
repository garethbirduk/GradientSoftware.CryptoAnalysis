using CryptoAnalysis.Csv.TypeConverters;
using CsvHelper.Configuration;
using Gradient.CryptoAnalysis;

namespace CryptoAnalysis.Csv.ClassMaps
{
    public class TradeResultClassMap : ClassMap<TradeResult>
    {
        public TradeResultClassMap()
        {
            Map(m => m.DateTimeOpen).Name("time").TypeConverter<UniversalTimeConverter>();
        }
    }
}