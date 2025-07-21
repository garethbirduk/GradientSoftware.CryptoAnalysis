using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis.Test.Conditions.PriceConditions
{
    [TestClass]
    public class IsInRangeConditionTests
    {
        private List<Price> _prices;
        public static readonly string _cryptoDataFilePath = Path.Combine("TestData", "Conditions", "PriceConditions", "IsInRangeConditionTests.csv");

        [TestMethod]
        public void Test1()
        {
            var conditionSet = new ConditionSet();
            conditionSet.AndConditions.Add(new IsInRangeCondition(10, 25));

            var date = new DateTime(2023, 02, 01);
            var dictionary = new Dictionary<DateTime, bool>();
            var list = new List<DateTime>();
            while (date < new DateTime(2024, 07, 01))
            {
                var result = conditionSet.IsMet(_prices, date);
                dictionary.Add(date, result);
                if (result)
                    list.Add(date);
                date = date.AddHours(1);
            }

            Assert.IsTrue(list.Count > 0);
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(_cryptoDataFilePath).
                Where(x => x.DateTime > new DateTime(2022, 12, 31)).ToList();
        }
    }
}