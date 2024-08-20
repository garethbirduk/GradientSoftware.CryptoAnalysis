using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis.Test.Conditions.PriceConditions
{
    [TestClass]
    public class IsWhiteBeltSystemConditionTests
    {
        private List<Price> _prices;
        public static readonly string _cryptoDataFilePath = Path.Combine("TestData", "Conditions", "PriceConditions", "IsWhiteBeltSystemConditionTests.csv");

        [TestMethod]
        public void Test1()
        {
            var condition = new ConditionSet();
            condition.AndConditions.Add(new IsWhiteBeltSystemCondition(4, 1, 50));

            var date = new DateTime(2023, 03, 12);
            var dictionary = new Dictionary<DateTime, bool>();
            var list = new List<DateTime>();
            while (date < new DateTime(2024, 07, 01))
            {
                var result = condition.IsMet(_prices, date);
                dictionary.Add(date, result);
                if (result)
                    list.Add(date);
                date = date.AddHours(1);
            }
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(_cryptoDataFilePath).
                Where(x => x.DateTime > new DateTime(2023, 02, 01)).ToList();
        }
    }
}