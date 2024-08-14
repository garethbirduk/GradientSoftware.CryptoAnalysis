using Gradient.CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis.Test.Conditions.PriceConditions
{
    [TestClass]
    public class IsConfirmedBreakOfStructureConditionTests
    {
        private List<Price> _prices;
        public static readonly string _cryptoDataFilePath = Path.Combine("TestData", "Conditions", "PriceConditions", "IsConfirmedBreakOfStructureConditionTestData.csv");

        [TestInitialize]
        public void TestInitialize()
        {
            _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(_cryptoDataFilePath).ToList();
        }

        [TestMethod]
        public void TestIsConfirmedBreakOfStructureCondition()
        {
            var condition = new Condition();
            condition.AndConditions.Add(new IsBreakOfStructureCondition());

            var expected = new List<DateTime>()
            {
                new DateTime(2024, 01, 01, 09, 00, 00),
                new DateTime(2024, 01, 01, 13, 00, 00),
                new DateTime(2024, 01, 01, 15, 00, 00),
                new DateTime(2024, 01, 01, 17, 00, 00),
                new DateTime(2024, 01, 01, 23, 00, 00),
                new DateTime(2024, 01, 02, 02, 00, 00),
                new DateTime(2024, 01, 02, 07, 00, 00),
            };

            foreach (var item in _prices)
            {
                Assert.AreEqual(expected.Contains(item.DateTime), condition.IsMet(_prices, item.DateTime));
            }
        }
    }
}