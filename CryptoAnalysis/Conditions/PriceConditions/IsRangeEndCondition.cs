namespace Gradient.CryptoAnalysis.Conditions.PriceConditions;

/// <summary>
/// Met on the candle a range at a sawtooth level ends on (see <see cref="Ranges"/>): the first close beyond a band. With a
/// direction, only an end that way counts: Up above the upper band, Down below the lower.
/// </summary>
public class IsRangeEndCondition : PriceCondition, IAdjustableCandles
{
    protected override bool IsMet()
    {
        return Ranges.At(PricesToCurrent(), Basis, Level, MinRetracement, Band)
            .Any(x => x.End?.Time == Price.DateTime && (Direction == null || (Direction == EnumSwingDirection.Up) == x.EndedAbove));
    }

    public IsRangeEndCondition(EnumSwingDirection? direction = null, int additionalCandles = DefaultAdditionalCandles, int level = 1,
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
