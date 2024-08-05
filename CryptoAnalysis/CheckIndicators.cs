namespace Gradient.CryptoAnalysis
{
    public class CheckIndicators
    {
        public string OutputFilepath { get; set; }
        public ConditionRules PositionRules { get; set; } = new();
        public List<Price> Prices { get; set; } = new();

        public DateTime StartDateTime { get; set; }

        public List<Trade> Trades { get; set; } = new();

        public List<Trade> Execute()
        {
            var met = new List<DateTime>();
            var index = Prices.FindIndex(x => x.DateTime >= StartDateTime);

            while (index > -1 && index < Prices.Count())
            {
                var d = Prices[index];
                var dateTime = d.DateTime;

                if (PositionRules.PreConditions.IsMet(Prices, dateTime))
                {
                    var trade = new Trade(Prices,
                        PositionRules.ConfirmationConditions, PositionRules.TakeProfitConditions,
                        PositionRules.StopLossConditions, PositionRules.ExpireConditions);
                    trade.DateTimeOpen = dateTime;
                    Trades.Add(trade);
                }
                index++;
            }

            var helper = new CsvReaderHelper();
            helper.WriteData<Trade, TradeMap>(OutputFilepath, Trades);

            return Trades;
        }
    }
}