namespace Gradient.CryptoAnalysis;

/// <summary>
/// Finds ranges at a sawtooth level (see <see cref="RangeOutline"/>). In a trend at the level, the move from the last swing's
/// extreme to the next swing's start is retraced; when the retracement has come back at least a minimum share of the way,
/// but not the whole way, and the counter-move's own structure one level finer then breaks, the price has failed in both
/// directions: a range between the start of the retracement and its furthest point. The range holds until a close beyond a
/// band either side of it, measured as a share of its height, so a break of structure inside the band does not end it.
/// Everything is read from the prices up to the last candle, so a range identified on one candle is found again on every
/// later one until it ends, unless a later new extreme redraws the levels.
/// </summary>
public static class Ranges
{
    public const double DefaultMinRetracement = 75;
    public const double DefaultBand = 20;

    /// <summary>
    /// Returns the ranges at a level, in the order they were identified: at most one holds at a time, so a retracement that
    /// qualifies while another range holds is passed over. The retracements, trends and finer breaks are the level's own
    /// (see <see cref="Sawtooth.Retracements"/>, <see cref="Sawtooth.Trends"/>) and the market structure breaks one level finer.
    /// </summary>
    public static List<RangeOutline> Find(List<Price> prices, EnumPriceBasis basis, IReadOnlyList<RetracementOutline> retracements,
        IReadOnlyList<SwingOutline> swings, IReadOnlyList<TrendOutline> trends, IReadOnlyList<MarketStructureBreakOutline> finerBreaks,
        double minRetracement = DefaultMinRetracement, double band = DefaultBand)
    {
        var index = new Dictionary<DateTime, int>();
        for (var i = 0; i < prices.Count; i++)
            index.TryAdd(prices[i].DateTime, i);

        var found = new List<RangeOutline>();
        foreach (var r in retracements.OrderBy(x => x.To.Time))
        {
            var setup = Identify(r);
            if (setup == null || (found.Count > 0 && (found[^1].End is not PricePoint end || end.Time >= setup.Identified.Time)))
                continue;
            found.Add(setup);
        }

        return found;

        RangeOutline? Identify(RetracementOutline r)
        {
            // The trend the range comes out of must hold where the retracement starts.
            if (!trends.Any(t => t.Direction == r.Direction && t.Confirmed.Time <= r.To.Time && (t.End == null || t.End.Time > r.To.Time)))
                return null;

            // The counter-move lasts until this swing breaks structure, the close beyond its start that continues the trend.
            var swing = r.Confirmed ? swings.FirstOrDefault(x => x.Direction == r.Direction && x.Start == r.To) : null;
            var limit = swing?.BreakOfStructure?.Time ?? DateTime.MaxValue;
            var against = r.Direction == EnumSwingDirection.Down ? EnumAnnotationType.BearishMarketStructureBreak : EnumAnnotationType.BullishMarketStructureBreak;

            foreach (var msb in finerBreaks.Where(b => b.Type == against && b.Reference.Time > r.To.Time && b.Break.Time < limit).OrderBy(b => b.Break.Time))
            {
                var deepest = r.Steps.LastOrDefault(s => s.Point.Time <= msb.Break.Time);
                if (deepest == null || deepest.Percent < minRetracement)
                    continue;
                if (deepest.Percent >= 100)
                    return null;

                var (low, high) = r.Direction == EnumSwingDirection.Down ? (r.To, deepest.Point) : (deepest.Point, r.To);
                var range = new RangeOutline(r.Level, r.Direction, r.From, low, high, msb.Break, deepest.Percent, band, null);
                return range with { End = EndOf(range, index[msb.Break.Time] + 1) };
            }

            return null;
        }

        PricePoint? EndOf(RangeOutline range, int from)
        {
            for (var i = from; i < prices.Count; i++)
            {
                var high = basis == EnumPriceBasis.Wick ? prices[i].High : prices[i].Close;
                var low = basis == EnumPriceBasis.Wick ? prices[i].Low : prices[i].Close;
                if (high > range.BandHigh)
                    return new PricePoint(prices[i].DateTime, high);
                if (low < range.BandLow)
                    return new PricePoint(prices[i].DateTime, low);
            }

            return null;
        }
    }

    /// <summary>
    /// Returns the ranges at a level from these prices alone, with hindsight: the sawtooth over the whole series.
    /// </summary>
    public static List<RangeOutline> At(List<Price> prices, EnumPriceBasis basis, int level, double minRetracement = DefaultMinRetracement, double band = DefaultBand)
    {
        var levels = Sawtooth.Levels(prices, basis, level + 1);
        if (level < 1 || level >= levels.Count)
            return [];

        var swings = Sawtooth.Swings(prices, levels, basis, level).OrderBy(x => x.Start.Time).ToList();
        var breaks = Sawtooth.MarketStructureBreaks(prices, swings, basis);
        var retracements = Sawtooth.Retracements(prices, swings, Sawtooth.Candidates(prices, levels, basis, level), basis);
        var finerBreaks = Sawtooth.MarketStructureBreaks(prices, Sawtooth.Swings(prices, levels, basis, level + 1), basis);
        return Find(prices, basis, retracements, swings, Sawtooth.Trends(swings, breaks), finerBreaks, minRetracement, band);
    }
}
