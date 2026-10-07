namespace Gradient.CryptoAnalysis.Conditions.PriceConditions;

/// <summary>
/// Met while an uptrend is ongoing at a sawtooth level, as seen from the prices up to the current candle.
/// </summary>
public class IsUptrendCondition : IsTrendCondition
{
    public IsUptrendCondition(int successiveCandles = DefaultAdditionalCandles, int level = 1, int minSwings = 2, int? maxMarketStructureBreaks = null)
        : base(EnumSwingDirection.Up, successiveCandles, level, minSwings, maxMarketStructureBreaks)
    {
    }
}
