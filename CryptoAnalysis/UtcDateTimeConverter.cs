using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Globalization;

namespace Gradient.CryptoAnalysis
{
    public class UtcDateTimeConverter : DateTimeConverter
    {
        private readonly string _format = "yyyy-MM-ddTHH:mm:ssZ";

        public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
        {
            if (DateTime.TryParse(text, out var dateTime))
            {
                return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
            }
            return base.ConvertFromString(text, row, memberMapData);
        }

        public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
        {
            if (value is DateTime dateTime)
            {
                return dateTime.ToUniversalTime().ToString(_format, CultureInfo.InvariantCulture);
            }

            return base.ConvertToString(value, row, memberMapData);
        }
    }
}