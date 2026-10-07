using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis.Test.Conditions.PriceConditions
{
    [TestClass]
    public class IsConfirmedMarketStructureBreakConditionTests
    {
        private List<Price> _prices;
        public static readonly string _cryptoDataFilePath = Path.Combine("TestData", "Conditions", "PriceConditions", "IsConfirmedMarketStructureBreakConditionTestData.csv");

        [TestInitialize]
        public void TestInitialize()
        {
            _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(_cryptoDataFilePath).ToList();
        }

        [TestMethod]
        public void TestIsConfirmedMarketStructureBreakCondition()
        {
            var condition = new ConditionSet();
            condition.AndConditions.Add(new IsMarketStructureBreakCondition());

            var expected = new List<DateTime>()
            {
                new DateTime(2024, 01, 01, 14, 00, 00),
                new DateTime(2024, 01, 02, 17, 00, 00),
            };

            foreach (var item in _prices)
            {
                Assert.AreEqual(expected.Contains(item.DateTime), condition.IsMet(_prices, item.DateTime));
            }
        }
    }
}