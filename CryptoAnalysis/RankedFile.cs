namespace Gradient.CryptoAnalysis
{
    public class RankedFile
    {
        public string FilePath { get; set; }
        public List<TradeResult> Results { get; set; }
        public double TotalProfit { get; set; }
    }
}