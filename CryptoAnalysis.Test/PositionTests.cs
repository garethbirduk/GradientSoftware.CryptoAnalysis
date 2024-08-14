using System.Text.Json;
using Gradient.CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis.Tests
{
    //[TestClass]
    public class PositionTests
    {
        public static List<RankedFile> GetResults(string folderPath)
        {
            // Get all JSON files in the folder
            var jsonFiles = Directory.GetFiles(folderPath, "*.json");

            // List to store the total profit for each file
            var rankedFiles = new List<RankedFile>();

            foreach (var file in jsonFiles)
            {
                // Read the file content
                var jsonString = File.ReadAllText(file);

                // Deserialize the JSON content to IEnumerable<TradeResult>
                var results = JsonSerializer.Deserialize<IEnumerable<TradeResult>>(jsonString);

                if (results != null)
                {
                    // Calculate the total profit for the current file
                    double totalProfit = results.Sum(r => r.Profit);

                    // Add the result to the ranked files list
                    rankedFiles.Add(new RankedFile
                    {
                        FilePath = file,
                        TotalProfit = totalProfit,
                        Results = results.ToList(),
                    });
                }
            }

            // Rank the files by total profit
            var rankedFilesSorted = rankedFiles.OrderByDescending(rf => rf.TotalProfit).ToList();

            return rankedFilesSorted;
        }

        [TestMethod]
        public void Test1()
        {
            var position = new ConditionRules();

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
            var positionRules = new ConditionRules();

            positionRules.PreConditions.AndConditions.Add(new IsSuccessiveGreenCandlesCondition(5));
            positionRules.ConfirmationConditions.AndConditions.Add(new IsSuccessiveGreenCandlesCondition(3));
            positionRules.ExpireConditions.AndConditions.Add(new IsSuccessiveRedCandlesCondition(3));
            positionRules.StopLossConditions.AndConditions.Add(new IsPriceLowLessThanOrEqualCondition(0.0));
            positionRules.TakeProfitConditions.AndConditions.Add(new IsPriceHighGreaterThanOrEqualCondition(100.0));

            var csvHelper = new CsvReaderHelper();
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var _cryptoDataFilePath = Path.Combine(baseDir, "TestData", "COINBASE_SPAUSD, 60.csv");
            var backtest = new Backtest()
            {
                PositionRules = positionRules,
                StartDateTime = new DateTime(2023, 12, 27),
                Prices = csvHelper.ReadData<Price, PriceClassMap>(_cryptoDataFilePath).ToList()
            };
            backtest.Execute();
        }

        [TestMethod]
        public void Test3()
        {
            var launcher = new BacktestLauncher();
            launcher.Launch();
        }

        [TestMethod]
        public void Test4()
        {
            var results = GetResults(Path.Combine("c:\\", "temp", "results"));
        }

        [TestMethod]
        public void TestRange()
        {
            var position = new ConditionRules();

            var isGoodWeekday = new Condition();
            isGoodWeekday.AndConditions.Add(new IsWeekdayCondition());
            isGoodWeekday.AndConditions.Add(new IsSuccessiveGreenCandlesCondition(3));
        }
    }
}