namespace Gradient.CryptoAnalysis
{
    public class CryptoCoinDataComparer : IEqualityComparer<Price>
    {
        public bool Equals(Price x, Price y)
        {
            if (x == null || y == null) return false;
            return x.Time == y.Time && x.Open == y.Open && x.High == y.High && x.Low == y.Low && x.Close == y.Close;
        }

        public int GetHashCode(Price obj)
        {
            if (obj == null) return 0;
            int hashTime = obj.Time.GetHashCode();
            int hashOpen = obj.Open.GetHashCode();
            int hashHigh = obj.High.GetHashCode();
            int hashLow = obj.Low.GetHashCode();
            int hashClose = obj.Close.GetHashCode();
            return hashTime ^ hashOpen ^ hashHigh ^ hashLow ^ hashClose;
        }
    }

    public class Price
    {
        public double Close { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Open { get; set; }
        public DateTime Time { get; set; }
    }
}