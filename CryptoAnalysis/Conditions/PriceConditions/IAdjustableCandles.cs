namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public interface IAdjustableCandles
    {
        public int AdditionalCandles { get; }

        public void SetAdditionalCandles(int additionalCandles);
    }
}