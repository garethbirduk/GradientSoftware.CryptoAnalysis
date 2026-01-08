namespace Gradient.CryptoAnalysis
{
    public class BacktestResult
    {
        public string Coin { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public double Entry { get; set; }
        public DateTime EntryDateTime { get; set; }
        public double Exit { get; set; }
        public DateTime ExitDateTime { get; set; }
        public double Returns { get; set; }
        public double StopLoss { get; set; }
        public string WinLoss { get; set; } = string.Empty;
    }
}