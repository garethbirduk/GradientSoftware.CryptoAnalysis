using Gradient.CryptoAnalysis.Conditions.PriceConditions;

namespace Gradient.CryptoAnalysis.Conditions.Strategies;

/// <summary>
/// The training's range trade, long only, as backtest rules built from the range conditions. While a range holds at a
/// level, a candle in the buy-low zone calls for a trade and fills it on that candle: by wick, a limit order at the top of
/// the zone, filled there (or at the candle's open when that is already inside the zone); by close, at the close. The take
/// profit is the bottom of the sell-high zone and the stop loss the lower band, both filled on the wick from the next
/// candle on. One trade at a time.
/// </summary>
public sealed class RangeLongStrategy
{
    public RangeLongStrategy(EnumPriceBasis entryTouch = EnumPriceBasis.Wick, int level = 1, double zone = 25, double minRetracement = Ranges.DefaultMinRetracement,
        double band = Ranges.DefaultBand, int candles = 500, EnumPriceBasis basis = EnumPriceBasis.Close)
    {
        EntryTouch = entryTouch;
        Level = level;
        Zone = zone;
        MinRetracement = minRetracement;
        Band = band;
        Candles = candles;
        Basis = basis;
    }

    public double Band { get; }

    public EnumPriceBasis Basis { get; }

    /// <summary>
    /// How many candles of history the conditions read, which is where their sawtooth starts.
    /// </summary>
    public int Candles { get; }

    public EnumPriceBasis EntryTouch { get; }

    public int Level { get; }

    public double MinRetracement { get; }

    public double Zone { get; }

    /// <summary>
    /// Builds the rules: the condition that calls for a trade, how it fills, where its targets are, and what ends it.
    /// </summary>
    public BacktestConditionRules Rules()
    {
        var rules = new BacktestConditionRules { OnePositionAtATime = true, Entry = EntryAt, Targets = TargetsAt };
        rules.PreConditions.AndConditions.Add(new IsInRangeZoneCondition(EnumRangeZone.BuyLow, EntryTouch, Candles, Level, Zone, MinRetracement, Band, Basis));
        rules.TakeProfitConditions.AndConditions.Add(new IsPriceHighGreaterThanOrEqualCondition(0));
        rules.StopLossConditions.AndConditions.Add(new IsPriceLowLessThanOrEqualCondition(0));
        return rules;
    }

    /// <summary>
    /// The fill on the candle that called for the trade: the top of the buy-low zone, or the open if that is lower, for a
    /// limit order; the close for a market order at the close.
    /// </summary>
    public double? EntryAt(List<Price> prices, DateTime dateTime)
    {
        var candle = prices.FirstOrDefault(x => x.DateTime == dateTime);
        var range = RangeAt(prices, dateTime);
        if (candle == null || range == null)
            return null;
        return EntryTouch == EnumPriceBasis.Close ? candle.Close : Math.Min(candle.Open, range.BuyLowTop(Zone));
    }

    /// <summary>
    /// The targets of a trade opening on a candle: the bottom of the sell-high zone and the lower band of the range there.
    /// Null when there is none, which leaves the trade at the default targets.
    /// </summary>
    public (double TakeProfit, double StopLoss)? TargetsAt(List<Price> prices, DateTime dateTime)
    {
        var range = RangeAt(prices, dateTime);
        return range == null ? null : (range.SellHighBottom(Zone), range.BandLow);
    }

    // The range the trade is in: the latest identified by the candle, counting one that ends on the candle itself, since the
    // trade was called for by the candle's reach into the zone before its close.
    private RangeOutline? RangeAt(List<Price> prices, DateTime dateTime)
    {
        var seen = prices.Where(x => x.DateTime <= dateTime).TakeLast(Candles + 1).ToList();
        return Ranges.At(seen, Basis, Level, MinRetracement, Band).LastOrDefault(x => x.Identified.Time <= dateTime && (x.End == null || x.End.Time >= dateTime));
    }
}
