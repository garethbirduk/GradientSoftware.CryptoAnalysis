using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Globalization;

namespace CryptoAnalysis.Csv.TypeConverters
{
    public class UniversalTimeConverter : ITypeConverter
    {
        public object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
        {
            if (DateTime.TryParse(text, null, DateTimeStyles.RoundtripKind, out DateTime dateTime))
            {
                return dateTime;
            }
            throw new TypeConverterException(this, memberMapData, text, row.Context, "Invalid date format.");
        }

        public string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
        {
            if (value is DateTime dateTime)
            {
                // Ensure the dateTime is in UTC
                var s = dateTime.ToString("o", CultureInfo.InvariantCulture);
                return s;
            }
            throw new TypeConverterException(this, memberMapData, value, row.Context, "Invalid date format.");
        }
    }
}