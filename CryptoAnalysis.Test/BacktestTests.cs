namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class BacktestTests
    {
        private string _cryptoDataFilePath = Path.Combine("TestData", "TestSimple.csv");
        public CoinCurrencyPairData CoinCurrencyPairData { get; set; }

        public DateTime EndDateTime { get; set; }

        public List<EnterTradeCondition> EnterTradeConditions { get; set; } = new();

        public DateTime StartDateTime { get; set; }

        public StopLossCalculation StopLossCalculation { get; set; } = new();

        public List<Trade> Trades { get; set; } = new();

        [TestMethod]
        public void Test1()
        {
            // Arrange
            var csvHelper = new CsvReaderHelper();

            // Act
            var data = csvHelper.ReadCryptoCoinData(_cryptoDataFilePath).ToList();

            var backtest = new Backtest()
            {
                StartDateTime = new DateTime(2024, 06, 01),
                CoinCurrencyPairData = new CoinCurrencyPairData()
                {
                    TimePeriods = new List<TimePeriodData>()
                    {
                        new TimePeriodData()
                        {
                            Interval = 60,
                            Data = data,
                        }
                    }
                },
                EnterTradeConditions = new List<EnterTradeCondition>()
                {
                    new PriceIncreaseRate(data, 8, 3)
                },
                StopLossCalculation = new StopLossCalculation()
                {
                    Data = data,
                },
                TakeProfitCalculation = new TakeProfitCalculation()
                {
                    Data = data,
                }
            };

            backtest.Execute(60);
        }
    }
}