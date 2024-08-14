namespace Gradient.CryptoAnalysis
{
    public class TradeResult
    {
        public TradeResult(Trade trade)
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