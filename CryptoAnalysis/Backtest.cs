using Gradient.CryptoAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gradient.CryptoAnalysis
{
    public static class ResultExtensions
    {
        public static void SaveToJson(this IEnumerable<Result> results, string filePath)
        {
            var folder = Path.GetDirectoryName(filePath);
            if (!Path.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters =
            {
                new JsonStringEnumConverter()
            }
            };

            string jsonString = JsonSerializer.Serialize(results, options);
            File.WriteAllText(filePath, jsonString);
        }
    }

    public class Analysis
    {
        internal void Analyse()
        {
            var xxxxx = Results.Select(x => x.Value).OrderBy(x => x.Select(y => y.Profit)).ToList();
        }

        public Dictionary<string, List<Result>> Results { get; } = new();
    }

    public class Backtest
    {
        public PositionRules PositionRules { get; set; } = new();
        public List<Price> Prices { get; set; } = new();

        public DateTime StartDateTime { get; set; }

        public List<Trade> Trades { get; set; } = new();

        public List<Result> Execute()
        {
            var met = new List<DateTime>();

            var index = Prices.FindIndex(x => x.DateTime >= StartDateTime);

            while (index > -1 && index < Prices.Count())
            {
                var d = Prices[index];
                var dateTime = d.DateTime.ToUniversalTime();

                foreach (var trade in Trades)
                {
                    trade.Update(dateTime);
                }

                if (PositionRules.PreConditions.IsMet(Prices, dateTime))
                {
                    var trade = new Trade(Prices,
                        PositionRules.ConfirmationConditions, PositionRules.TakeProfitConditions,
                        PositionRules.StopLossConditions, PositionRules.ExpireConditions);

                    Trades.Add(trade);
                }

                index++;
            }

            var completed = Trades.Where(x => x.TradeStatus == EnumTradeStatus.Completed).ToList();
            var won = completed.Where(x => x.PriceClose > x.PriceOpen).Select(x => new { x.Id, x.DateTimeOpen, x.DateTimeClose, x.PriceOpen, x.PriceClose, x.TakeProfitTarget, x.StopLossTarget }).ToList();
            var lost = completed.Where(x => x.PriceClose < x.PriceOpen).Select(x => new { x.Id, x.DateTimeOpen, x.DateTimeClose, x.PriceOpen, x.PriceClose, x.TakeProfitTarget, x.StopLossTarget }).ToList();

            var profits = completed.Sum(x => x.PriceClose - x.PriceOpen);

            return Trades.Where(x => x.TradeStatus == EnumTradeStatus.Completed).Select(x => new Result(x)).ToList();
        }
    }

    public class BacktestLauncher
    {
        private Analysis Analysis { get; set; } = new Analysis();
        private Dictionary<string, List<Result>> Results { get; set; } = new Dictionary<string, List<Result>>();

        public void Launch()
        {
            for (int g1 = 3; g1 < 6; g1++)
            {
                for (int g2 = 3; g2 < 6; g2++)
                {
                    for (int r1 = 3; r1 < 6; r1++)
                    {
                        var positionRules = new PositionRules();

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
                            Prices = csvHelper.ReadCryptoCoinData(_cryptoDataFilePath).ToList()
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

    public class RankedFile
    {
        public string FilePath { get; set; }
        public List<Result> Results { get; set; }
        public double TotalProfit { get; set; }
    }

    public class Result
    {
        public Result()
        {
        }

        public Result(Trade trade)
        {
            DateTimeClose = trade.DateTimeClose;
            DateTimeOpen = trade.DateTimeOpen;
            Profit = trade.PriceClose - trade.PriceOpen;
        }

        public DateTime DateTimeClose { get; set; }
        public DateTime DateTimeOpen { get; set; }
        public Guid Id { get; } = Guid.NewGuid();
        public double Profit { get; set; }
    }
}