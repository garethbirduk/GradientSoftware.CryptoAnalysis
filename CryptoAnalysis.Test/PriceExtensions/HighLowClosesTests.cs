using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions
{
    [TestClass]
    public class HighLowClosesTests
    {
        private List<Price> _prices;
        public static readonly string _cryptoDataFilePath = Path.Combine("TestData", "PricesExtensionsData", "HighLowClosesTests", "TestData.csv");

        [TestMethod]
        public void TestGetHighs()
        {
            CollectionAssert.AreEqual(new List<Price>(), new List<Price>().HighCloses());

            var highs = _prices.HighCloses();
            Assert.AreEqual(16, highs.Count);
            var i = 0;
            Assert.AreEqual(new DateTime(2024, 2, 7), highs[i++].DateTime);
            Assert.AreEqual(new DateTime(2024, 2, 7, 9, 0, 0), highs[i++].DateTime);
            Assert.AreEqual(new DateTime(2024, 2, 7, 13, 0, 0), highs[i++].DateTime);
            Assert.AreEqual(new DateTime(2024, 2, 7, 16, 0, 0), highs[i++].DateTime);
            Assert.AreEqual(new DateTime(2024, 2, 7, 17, 0, 0), highs[i++].DateTime);
            Assert.AreEqual(new DateTime(2024, 2, 7, 18, 0, 0), highs[i++].DateTime);
        }

        [TestMethod]
        public void TestGetLows()
        {
            CollectionAssert.AreEqual(new List<Price>(), new List<Price>().LowCloses());

            var lows = _prices.LowCloses();
            Assert.AreEqual(4, lows.Count);
            var i = 0;
            Assert.AreEqual(new DateTime(2024, 2, 7), lows[i++].DateTime);
            Assert.AreEqual(new DateTime(2024, 2, 7, 1, 0, 0), lows[i++].DateTime);
            Assert.AreEqual(new DateTime(2024, 2, 7, 2, 0, 0), lows[i++].DateTime);
            Assert.AreEqual(new DateTime(2024, 2, 7, 3, 0, 0), lows[i++].DateTime);
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(_cryptoDataFilePath).ToList();
        }
    }
}