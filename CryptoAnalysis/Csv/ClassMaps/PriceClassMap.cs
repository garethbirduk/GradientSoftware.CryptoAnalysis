using CryptoAnalysis.Csv.TypeConverters;
using CsvHelper.Configuration;
using Gradient.CryptoAnalysis;
using Gradient.CryptoAnalysis.Csv;
using System.Globalization;

namespace CryptoAnalysis.Csv.ClassMaps
{
    public class PriceClassMap : ClassMap<Price>
    {
        public PriceClassMap()
        {
            AutoMap(CultureInfo.InvariantCulture);
            Map(m => m.DateTime).Name("time").TypeConverter<UniversalTimeConverter>();
            Map(m => m.Close).Name("close").NameIndex(0).Validate(args => !string.IsNullOrEmpty(args.Field));
            Map(m => m.Open).Name("open").NameIndex(0).Validate(args => !string.IsNullOrEmpty(args.Field));
            Map(m => m.High).Name("high").NameIndex(0).Validate(args => !string.IsNullOrEmpty(args.Field));
            Map(m => m.Low).Name("low").NameIndex(0).Validate(args => !string.IsNullOrEmpty(args.Field));
            Map(m => m.Indicators).TypeConverter<IndicatorsConverter>();
        }
    }
}