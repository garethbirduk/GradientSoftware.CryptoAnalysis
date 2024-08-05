using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gradient.CryptoAnalysis
{
    public static class ResultExtensions
    {
        public static void SaveToJson(this IEnumerable<TradeResult> results, string filePath)
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

        public Dictionary<string, List<TradeResult>> Results { get; } = new();
    }

    public class Backtest
    {
        public PositionRules PositionRules { get; set; } = new();
        public List<Price> Prices { get; set; } = new();

        public DateTime StartDateTime { get; set; }

        public List<Trade> Trades { get; set; } = new();

        public List<TradeResult> Execute()
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

            return Trades.Where(x => x.TradeStatus == EnumTradeStatus.Completed).Select(x => new TradeResult(x)).ToList();
        }
    }
}