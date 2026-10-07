namespace Gradient.CryptoAnalysis.Conditions.PriceConditions;

/// <summary>
/// Met while a range holds at a sawtooth level, as seen from the prices up to the current candle (see <see cref="Ranges"/>):
/// identified by a retracement of at least minRetracement percent whose structure broke one level finer, and not yet ended by a
/// close beyond its band. With a direction, only a range out of a trend that way counts.
/// </summary>
public class IsRangeHoldingCondition : PriceCondition, IAdjustableCandles
{
    protected override bool IsMet()
    {
        return Ranges.At(PricesToCurrent(), Basis, Level, MinRetracement, Band)
            .Any(x => x.End == null && (Direction == null || x.Direction == Direction));
    }

    public IsRangeHoldingCondition(EnumSwingDirection? direction = null, int additionalCandles = DefaultAdditionalCandles, int level = 1,
        double minRetracement = Ranges.DefaultMinRetracement, double band = Ranges.DefaultBand, EnumPriceBasis basis = EnumPriceBasis.Close) : base(additionalCandles)
    {
        Direction = direction;
        Level = level;
        MinRetracement = minRetracement;
        Band = band;
        Basis = basis;
    }

    public double Band { get; }

    public EnumPriceBasis Basis { get; }

    public EnumSwingDirection? Direction { get; }

    public int Level { get; }

    public double MinRetracement { get; }

    /// <inheritdoc/>
    public void SetAdditionalCandles(int additionalCandles)
    {
        AdditionalCandles = additionalCandles;
    }
}
