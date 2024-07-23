using System;

namespace Gradient.CryptoAnalysis
{
    public class BacktestResult
    {
        public string Coin { get; set; }
        public string Direction { get; set; }
        public double Entry { get; set; }
        public DateTime EntryDateTime { get; set; }
        public double Exit { get; set; }
        public DateTime ExitDateTime { get; set; }
        public double Returns { get; set; }
        public double StopLoss { get; set; }
        public string WinLoss { get; set; }
    }
}