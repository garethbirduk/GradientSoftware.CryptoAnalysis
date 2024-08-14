using CryptoAnalysis.Conditions;

namespace Gradient.CryptoAnalysis
{
    public class Backtest
    {
        public BacktestConditionRules PositionRules { get; set; } = new();
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
                var dateTime = d.DateTime;

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

            var completed = Trades.Where(x => x.TradeStatus == EnumConditionStatus.Completed).ToList();
            var won = completed.Where(x => x.PriceClose > x.PriceOpen).Select(x => new { x.Id, x.DateTimeOpen, x.DateTimeClose, x.PriceOpen, x.PriceClose, x.TakeProfitTarget, x.StopLossTarget }).ToList();
            var lost = completed.Where(x => x.PriceClose < x.PriceOpen).Select(x => new { x.Id, x.DateTimeOpen, x.DateTimeClose, x.PriceOpen, x.PriceClose, x.TakeProfitTarget, x.StopLossTarget }).ToList();

            var profits = completed.Sum(x => x.PriceClose - x.PriceOpen);

            return Trades.Where(x => x.TradeStatus == EnumConditionStatus.Completed).Select(x => new TradeResult(x)).ToList();
        }
    }
}