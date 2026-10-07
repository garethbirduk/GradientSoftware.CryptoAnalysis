namespace Gradient.CryptoAnalysis.Conditions.PriceConditions;

/// <summary>
/// Met while a trend in the given direction is ongoing at a sawtooth level, as seen from the prices up to the current candle
/// (see <see cref="MarketStructure"/>): confirmed by at least minSwings swings, not yet ended by a swing the other way, and with
/// no more than maxMarketStructureBreaks market structure breaks against it when that is set.
/// </summary>
public class IsTrendCondition : PriceCondition, IAdjustableCandles
{
    protected override bool IsMet()
    {
        var structure = MarketStructure.At(PricesToCurrent(), Basis, Level, MinSwings);
        return structure.Trends.Any(x => x.Direction == Direction && x.End == null
            && (MaxMarketStructureBreaks is not int max || x.MarketStructureBreaks <= max));
    }

    public IsTrendCondition(EnumSwingDirection direction, int additionalCandles = DefaultAdditionalCandles, int level = 1, int minSwings = 2,
        int? maxMarketStructureBreaks = null, EnumPriceBasis basis = EnumPriceBasis.Close) : base(additionalCandles)
    {
        Direction = direction;
        Level = level;
        MinSwings = minSwings;
        MaxMarketStructureBreaks = maxMarketStructureBreaks;
        Basis = basis;
    }

    public EnumPriceBasis Basis { get; }

    public EnumSwingDirection Direction { get; }

    public int Level { get; }

    public int? MaxMarketStructureBreaks { get; }

    public int MinSwings { get; }

    /// <inheritdoc/>
    public void SetAdditionalCandles(int additionalCandles)
    {
        AdditionalCandles = additionalCandles;
    }
}
