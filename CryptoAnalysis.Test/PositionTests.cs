using CryptoAnalysis;

namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class PositionTests
    {
        [TestMethod]
        public void Test1()
        {
            var position = new PositionRules();

            var isGoodWeekday = new Condition();
            isGoodWeekday.AndConditions.Add(new IsWeekdayCondition());
            isGoodWeekday.AndConditions.Add(new IsSuccessiveGreenCandlesCondition(3));

            var isBadWeekend = new Condition();
            isBadWeekend.AndConditions.Add(new IsWeekendCondition());
            isBadWeekend.AndConditions.Add(new IsSuccessiveRedCandlesCondition(3));

            var isGoodWeekdayOrIsBadWeekend = new Condition();
            isGoodWeekdayOrIsBadWeekend.OrSubConditions.Add(isGoodWeekday);
            isGoodWeekdayOrIsBadWeekend.OrSubConditions.Add(isBadWeekend);

            var isInJanuary = new Condition();
            isInJanuary.AndConditions.Add(new IsAfterDateCondition(new DateTime(2024, 01, 01)));
            isInJanuary.AndConditions.Add(new IsBeforeDateCondition(new DateTime(2024, 02, 01)));

            position.PreConditions.AndSubConditions.Add(isGoodWeekdayOrIsBadWeekend);
            position.PreConditions.AndSubConditions.Add(isInJanuary);

            var data = TestHelper.CreatePriceData(new DateTime(2024, 01, 01), 60, 100 * 24);

            var xx = 1;
        }

        [TestMethod]
        public void Test2()
        {
            var positionRules = new PositionRules();

            //positionRules.PreConditions.AndConditions.Add(new IsSuccessiveGreenCandlesCondition(6));
            positionRules.PreConditions.AndConditions.Add(new IsPriceIncreaseRateCondition(25, 10, true));

            var csvHelper = new CsvReaderHelper();
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var _cryptoDataFilePath = Path.Combine(baseDir, "TestData", "COINBASE_SPAUSD, 60.csv");
            var backtest = new Backtest()
            {
                PositionRules = positionRules,
                StartDateTime = new DateTime(2023, 12, 25),
                Prices = csvHelper.ReadCryptoCoinData(_cryptoDataFilePath).ToList()
            };
            backtest.Execute();
        }
    }
}