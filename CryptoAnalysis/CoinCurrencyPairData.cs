using System.Collections.Generic;

namespace Gradient.CryptoAnalysis
{
    public class CoinCurrencyPairData
    {
        public string Coin { get; set; }
        public string Currency { get; set; }
        public string Exchange { get; set; }
        public List<TimePeriodData> TimePeriods { get; set; } = new List<TimePeriodData>();
    }
}