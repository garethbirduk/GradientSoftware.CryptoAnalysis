namespace Gradient.CryptoAnalysis.Conditions.PriceConditions
{
    public interface IAdjustableCandles
    {
        public int AdditionalCandles { get; }

        /// <summary>
        /// Sets how many candles before the current one the condition may look back over.
        /// </summary>
        public void SetAdditionalCandles(int additionalCandles);
    }
}