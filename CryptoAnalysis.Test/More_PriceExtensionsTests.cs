namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class More_PriceExtensionsTests
    {
        private List<Price> _prices;
        public static readonly string _cryptoDataFilePath = Path.Combine("TestData", "PricesExtensionsData", "COINBASE_BTCUSD, 60", "COINBASE_BTCUSD, 60.csv");

        [TestMethod]
        public void TestGetHighLows()
        {
            var highs = _prices.HighCloses();
            var lows = _prices.LowCloses();

            var highIndices = highs.Select(x => _prices.IndexOf(x)).ToList();
            var lowIndices = lows.Select(x => _prices.IndexOf(x)).ToList();

            var hh = new List<Price>();
            var ll = new List<Price>();

            var index = 0;

            var hl = new List<Price>()
            {
                highs.First()
            };

            var highIndex = 0;
            var lowIndex = 0;

            var direction = "up";
            var newHigh = highs.First();
            while (index < _prices.Count() - 1)
            {
                if (highIndices.Contains(index))
                {
                    if (direction == "up")
                    {
                        newHigh = _prices[index];
                    }
                    direction = "up";
                }
                else if (lowIndices.Contains(index))
                {
                    direction = "down";
                }

                if (direction == "up")
                {
                }
            }
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