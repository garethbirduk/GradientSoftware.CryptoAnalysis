using CryptoAnalysis.Csv;

namespace Gradient.CryptoAnalysis
{
    public class BacktestLauncher
    {
        private Analysis Analysis { get; set; } = new Analysis();
        private Dictionary<string, List<TradeResult>> Results { get; set; } = new Dictionary<string, List<TradeResult>>();

        public void Launch()
        {
            for (int g1 = 3; g1 < 6; g1++)
            {
                for (int g2 = 3; g2 < 6; g2++)
                {
                    for (int r1 = 3; r1 < 6; r1++)
                    {
                        var positionRules = new ConditionRules();

                        positionRules.PreConditions.AndConditions.Add(new IsSuccessiveGreenCandlesCondition(g1));
                        positionRules.ConfirmationConditions.AndConditions.Add(new IsSuccessiveGreenCandlesCondition(g2));
                        positionRules.ExpireConditions.AndConditions.Add(new IsSuccessiveRedCandlesCondition(r1));
                        positionRules.StopLossConditions.AndConditions.Add(new IsPriceLowLessThanOrEqualCondition(0.0));
                        positionRules.TakeProfitConditions.AndConditions.Add(new IsPriceHighGreaterThanOrEqualCondition(100.0));

                        var csvHelper = new CsvReaderHelper();
                        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        var _cryptoDataFilePath = Path.Combine(baseDir, "TestData", "COINBASE_SPAUSD, 60.csv");
                        var backtest = new Backtest()
                        {
                            PositionRules = positionRules,
                            StartDateTime = new DateTime(2023, 05, 01),
                            Prices = csvHelper.ReadData<Price, PriceClassMap>(_cryptoDataFilePath).ToList()
                        };

                        var results = backtest.Execute();

                        var folder = Path.Combine("c:\\", "temp", "results");
                        var filename = $"g1_g2_r1 {g1}_{g2}_{r1} COINBASE_SPAUSD, 60.json";
                        results.SaveToJson(Path.Combine(folder, filename));

                        Analysis.Results.Add($"g1_g2_r1: {g1}_{g2}_{r1}", results);
                    }
                }
            }

            Analysis.Analyse();
        }
    }
}