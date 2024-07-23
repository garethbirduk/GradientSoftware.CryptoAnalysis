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

            position.Conditions.AndSubConditions.Add(isGoodWeekdayOrIsBadWeekend);
            position.Conditions.AndSubConditions.Add(isInJanuary);

            var data = TestHelper.CreatePriceData(new DateTime(2024, 01, 01), 60, 100 * 24);

            var xx = 1;
        }

        [TestMethod]
        public void Test2()
        {
            var positionRules = new PositionRules();

            var isGoodDay = new Condition();
            isGoodDay.AndConditions.Add(new IsSuccessiveGreenCandlesCondition(6));

            var isBadDay = new Condition();
            isBadDay.AndConditions.Add(new IsSuccessiveRedCandlesCondition(6));

            positionRules.Conditions.OrSubConditions.Add(isGoodDay);
            positionRules.Conditions.OrSubConditions.Add(isBadDay);

            var csvHelper = new CsvReaderHelper();
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var _cryptoDataFilePath = Path.Combine(baseDir, "TestData", "COINBASE_SPAUSD, 60.csv");
            var backtest = new Backtest2()
            {
                PositionRules = positionRules,
                StartDateTime = new DateTime(2023, 12, 25),
                Prices = csvHelper.ReadCryptoCoinData(_cryptoDataFilePath).ToList()
            };
            backtest.Execute();
        }
    }
}