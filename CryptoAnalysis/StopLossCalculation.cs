namespace Gradient.CryptoAnalysis
{
    public class StopLossCalculation
    {
        public List<Price> Data { get; set; }

        public double Calculate(int index)
        {
            return Data[index].Open / 2.0;
        }
    }
}