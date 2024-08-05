namespace Gradient.CryptoAnalysis
{
    public class TakeProfitCalculation
    {
        public List<Price> Data { get; set; }

        public double Calculate(int index)
        {
            var tp = Data[index].Open * 1.5;
            return tp;
        }
    }
}