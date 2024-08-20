using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions
{
    [TestClass]
    public class ToDownswingsTests
    {
        private List<Price> _prices;

        public static readonly string _cryptoDataFilePath = Path.Combine("TestData", "PricesExtensionsData", "Downswing.csv");

        [TestMethod]
        public void TestFindBreaksOfStructures()
        {
            // Act
            var actual = _prices.ToDownswings();

            // Assert
            CollectionAssert.AreEqual(new List<Downswing>(), new List<Price>().ToDownswings());

            Assert.AreEqual(7, actual.Count);

            Assert.AreEqual(8, actual[0].Prices.Count);
            Assert.AreEqual(_prices[11], actual[0].BreakOfStructure);
            Assert.IsNull(actual[0].MarketStructureBreak);

            Assert.AreEqual(3, actual[1].Prices.Count);
            Assert.AreEqual(_prices[16], actual[1].BreakOfStructure);
            Assert.IsNull(actual[1].MarketStructureBreak);

            Assert.AreEqual(4, actual[2].Prices.Count);
            Assert.AreEqual(_prices[20], actual[2].BreakOfStructure);
            Assert.AreEqual(_prices[17], actual[2].MarketStructureBreak);

            Assert.AreEqual(6, actual[3].Prices.Count);
            Assert.AreEqual(_prices[30], actual[3].BreakOfStructure);
            Assert.IsNull(actual[3].MarketStructureBreak);

            Assert.AreEqual(2, actual[4].Prices.Count);
            Assert.AreEqual(_prices[32], actual[4].BreakOfStructure);
            Assert.IsNull(actual[4].MarketStructureBreak);

            Assert.AreEqual(2, actual[5].Prices.Count);
            Assert.AreEqual(_prices[34], actual[5].BreakOfStructure);
            Assert.IsNull(actual[5].MarketStructureBreak);

            Assert.AreEqual(3, actual[6].Prices.Count);
            Assert.AreEqual(_prices[37], actual[6].BreakOfStructure);
            Assert.IsNull(actual[6].MarketStructureBreak);

            var interim2 = actual[2].InterimDownswings;

            Assert.AreEqual(1, interim2.Count);

            Assert.AreEqual(2, interim2[0].Prices.Count);
            Assert.AreEqual(_prices[19], interim2[0].BreakOfStructure);
            Assert.IsNull(interim2[0].MarketStructureBreak);
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(_cryptoDataFilePath).ToList();
        }
    }
}