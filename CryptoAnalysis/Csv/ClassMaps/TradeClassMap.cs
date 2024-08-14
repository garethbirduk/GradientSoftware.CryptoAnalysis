using CryptoAnalysis.Csv.TypeConverters;
using CsvHelper.Configuration;
using Gradient.CryptoAnalysis;

namespace CryptoAnalysis.Csv.ClassMaps
{
    public class TradeClassMap : ClassMap<Trade>
    {
        public TradeClassMap()
        {
            Map(m => m.DateTimeOpen).Name("time").TypeConverter<UniversalTimeConverter>();
        }
    }
}