using CryptoAnalysis;

namespace Gradient.CryptoAnalysis
{
    //public class Backtest
    //{
    //    public string Coin { get; set; }
    //    public CoinCurrencyPairData CoinCurrencyPairData { get; set; }
    //    public DateTime EndDateTime { get; set; }
    //    public List<EnterTradeCondition> EnterTradeConditions { get; set; } = new();
    //    public PositionRules PositionRules { get; set; } = new();
    //    public DateTime StartDateTime { get; set; }
    //    public StopLossCalculation StopLossCalculation { get; set; } = new();
    //    public TakeProfitCalculation TakeProfitCalculation { get; set; } = new();
    //    public List<Trade> Trades { get; set; } = new();

    //    public void Execute(int interval)
    //    {
    //        var data = CoinCurrencyPairData.TimePeriods.SingleOrDefault(x => x.Interval == interval)?.Data;
    //        if (data == null)
    //            return;

    //        var index = data.FindIndex(x => x.Time >= StartDateTime);

    //        while (index > -1 && index < data.Count())
    //        {
    //            var d = data[index];
    //            var dateTime = d.Time.ToUniversalTime();

    //            if (PositionRules.IsMet(data, dateTime))
    //            {
    //                // enterTrade
    //            }

    //            if (EnterTradeConditions.All(x => x.IsMet(index)))
    //            {
    //                var xxx = EnterTradeConditions.All(x => x.IsMet(index));
    //                Trades.Add(new Trade(Coin, d.Open, d.Time, StopLossCalculation.Calculate(index), TakeProfitCalculation.Calculate(index)));
    //            }

    //            foreach (var trade in Trades.Where(x => x.ExitDateTime == null &&
    //                (x.StopLoss >= d.Close || x.TakeProfit <= d.Close)))
    //            {
    //                trade.Close(d.Time, d.Close);
    //            }

    //            index++;
    //        }

    //        var closed = Trades.Where(x => x.ExitDateTime != null).ToList();

    //        var bought = closed.Select(x => x.ExitPrice - x.EntryPrice).ToList();

    //        var dates = closed.Select(x => $"{x.EntryDateTime}\t{x.ExitDateTime}").ToList();
    //        var profit = bought.Sum();
    //    }
    //}

    public class Backtest
    {
        public PositionRules PositionRules { get; set; } = new();
        public List<Price> Prices { get; set; } = new();

        public DateTime StartDateTime { get; set; }

        public List<Trade> Trades { get; set; } = new();

        public void Execute()
        {
            var met = new List<DateTime>();

            var index = Prices.FindIndex(x => x.Time >= StartDateTime);

            while (index > -1 && index < Prices.Count())
            {
                var d = Prices[index];
                var dateTime = d.Time.ToUniversalTime();

                foreach (var trade in Trades.Where(x => x.TradeStatus == EnumTradeStatus.Open)) { }

                if (PositionRules.PreConditions.IsMet(Prices, dateTime))
                {
                    var trade = new Trade(Prices,
                        PositionRules.ConfirmationConditions, PositionRules.TakeProfitConditions,
                        PositionRules.StopLossConditions, PositionRules.ExpireConditions);

                    //, StopLossCalculation.Calculate(index), TakeProfitCalculation.Calculate(index))

                    Trades.Add(trade);

                    met.Add(dateTime);
                }

                index++;
            }

            var bp = 1;
        }
    }
}