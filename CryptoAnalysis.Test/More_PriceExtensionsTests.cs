using CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis.Tests
{
    //[TestClass]
    public class More_PriceExtensionsTests
    {
        private List<Price> _prices;
        public static readonly string _cryptoDataFilePath = Path.Combine("TestData", "PricesExtensionsData", "COINBASE_BTCUSD, 60", "COINBASE_BTCUSD, 60.csv");

        [TestMethod]
        public void TestGetHighLows()
        {
        }

        [TestMethod]
        public void TestGetHighs()
        {
            var highs = _prices.HighCloses();
        }

        [TestMethod]
        public void TestGetLows()
        {
            var lows = _prices.LowCloses();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(_cryptoDataFilePath).ToList();
        }
    }
}