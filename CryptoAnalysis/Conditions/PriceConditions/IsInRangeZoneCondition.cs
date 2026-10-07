namespace Gradient.CryptoAnalysis.Conditions.PriceConditions;

/// <summary>
/// The two ends of a range where the training trades it: the buy-low zone from the lower band up to a share of the height
/// above the low (the discount), and the sell-high zone from as far below the high up to the upper band (the premium).
/// </summary>
public enum EnumRangeZone
{
    BuyLow,
    SellHigh,
}

/// <summary>
/// Met while a range holds at a sawtooth level (see <see cref="Ranges"/>) and the current candle is in one of its zones: by
/// close, the close is inside the zone; by wick, the candle reaches into it (its low down into the buy-low zone, its high up
/// into the sell-high zone). The zone's depth is a percentage of the range's height from its edge; each zone runs out to the band.
/// </summary>
public class IsInRangeZoneCondition : PriceCondition, IAdjustableCandles
{
    protected override bool IsMet()
    {
        var range = Ranges.At(PricesToCurrent(), Basis, Level, MinRetracement, Band).LastOrDefault(x => x.HoldsAt(Price.DateTime));
        if (range == null)
            return false;

        var (bottom, top) = Zone == EnumRangeZone.BuyLow ? (range.BandLow, range.BuyLowTop(ZonePercent)) : (range.SellHighBottom(ZonePercent), range.BandHigh);
        if (Touch == EnumPriceBasis.Close)
            return Price.Close >= bottom && Price.Close <= top;
        return Zone == EnumRangeZone.BuyLow ? Price.Low <= top : Price.High >= bottom;
    }

    public IsInRangeZoneCondition(EnumRangeZone zone, EnumPriceBasis touch = EnumPriceBasis.Close, int additionalCandles = DefaultAdditionalCandles, int level = 1,
        double zonePercent = 25, double minRetracement = Ranges.DefaultMinRetracement, double band = Ranges.DefaultBand, EnumPriceBasis basis = EnumPriceBasis.Close) : base(additionalCandles)
    {
        Zone = zone;
        Touch = touch;
        Level = level;
        ZonePercent = zonePercent;
        MinRetracement = minRetracement;
        Band = band;
        Basis = basis;
    }

    public double Band { get; }

    /// <summary>
    /// The price basis the structure is read on; Touch is how the candle is tested against the zone.
    /// </summary>
    public EnumPriceBasis Basis { get; }

    public int Level { get; }

    public double MinRetracement { get; }

    public EnumPriceBasis Touch { get; }

    public EnumRangeZone Zone { get; }

    public double ZonePercent { get; }

    /// <inheritdoc/>
    public void SetAdditionalCandles(int additionalCandles)
    {
        AdditionalCandles = additionalCandles;
    }
}
