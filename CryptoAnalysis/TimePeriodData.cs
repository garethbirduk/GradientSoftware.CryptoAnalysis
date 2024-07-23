using System.Collections.Generic;

namespace Gradient.CryptoAnalysis
{
    public class TimePeriodData
    {
        public List<Price> Data { get; set; } = new List<Price>();
        public int Interval { get; set; }
    }
}