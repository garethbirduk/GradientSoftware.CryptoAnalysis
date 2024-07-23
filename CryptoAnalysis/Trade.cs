namespace Gradient.CryptoAnalysis
{
    public class Trade
    {
        internal void Close(DateTime time, double close)
        {
            ExitDateTime = time;
            ExitPrice = close;
        }

        public Trade(string coin, double entry, DateTime entryDateTime, double stopLoss, double takeProfit)
        {
            Coin = coin;
            EntryPrice = entry;
            EntryDateTime = entryDateTime;
            StopLoss = stopLoss;
            TakeProfit = takeProfit;
        }

        public string Coin { get; set; }
        public string Direction { get; set; }
        public DateTime EntryDateTime { get; set; }
        public double EntryPrice { get; set; }
        public DateTime? ExitDateTime { get; set; }
        public double ExitPrice { get; set; }
        public double Returns { get; set; }
        public double StopLoss { get; set; }
        public double TakeProfit { get; set; }
        public string WinLoss { get; set; }
    }
}