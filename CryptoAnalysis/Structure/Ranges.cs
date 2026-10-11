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
        double minRetracement = DefaultMinRetracement, double band = DefaultBand) =>
        Find(prices, basis, retracements, swings, trends, finerBreaks, minRetracement, band, null, null);

    /// <summary>
    /// The ranges, as <see cref="Find(List{Price}, EnumPriceBasis, IReadOnlyList{RetracementOutline}, IReadOnlyList{SwingOutline}, IReadOnlyList{TrendOutline}, IReadOnlyList{MarketStructureBreakOutline}, double, double)"/>
    /// gives them, with what a caller that asks candle after candle already has: the candle of each time, and where each
    /// range it has met was read to when looking for its end, so that is read on from there and not from its start.
    /// </summary>
    internal static List<RangeOutline> Find(List<Price> prices, EnumPriceBasis basis, IReadOnlyList<RetracementOutline> retracements,
        IReadOnlyList<SwingOutline> swings, IReadOnlyList<TrendOutline> trends, IReadOnlyList<MarketStructureBreakOutline> finerBreaks,
        double minRetracement, double band, IReadOnlyDictionary<DateTime, int>? index, Dictionary<RangeOutline, (int Read, PricePoint? End)>? ends)
    {
        Dictionary<DateTime, int>? made = null;
        int IndexOf(DateTime time)
        {
            if (index != null)
                return index[time];
            if (made == null)
            {
                made = [];
                for (var i = 0; i < prices.Count; i++)
                    made.TryAdd(prices[i].DateTime, i);
            }

            return made[time];
        }

        // The trends follow one another, each ended before the next is confirmed, so the one holding at a time is the last
        // confirmed by then; were they given in another order, each is looked at.
        var inOrder = true;
        for (var i = 1; i < trends.Count && inOrder; i++)
            inOrder = trends[i - 1].Confirmed.Time <= trends[i].Confirmed.Time && trends[i - 1].End != null && trends[i - 1].End!.Time <= trends[i].Confirmed.Time;
        var confirmed = trends.Select(x => x.Confirmed.Time).ToList();
        bool Holds(EnumSwingDirection direction, DateTime time)
        {
            if (!inOrder)
                return trends.Any(t => t.Direction == direction && t.Confirmed.Time <= time && (t.End == null || t.End.Time > time));
            var at = Sawtooth.CountUpTo(confirmed, time) - 1;
            return at >= 0 && trends[at].Direction == direction && (trends[at].End == null || trends[at].End!.Time > time);
        }

        // The first swing each way from each start, and the finer breaks each way in the order they broke.
        var byStart = new Dictionary<(EnumSwingDirection, PricePoint), SwingOutline>();
        foreach (var swing in swings)
            byStart.TryAdd((swing.Direction, swing.Start), swing);
        var bullish = finerBreaks.Where(b => b.Type == EnumAnnotationType.BullishMarketStructureBreak).OrderBy(b => b.Break.Time).ToList();
        var bearish = finerBreaks.Where(b => b.Type == EnumAnnotationType.BearishMarketStructureBreak).OrderBy(b => b.Break.Time).ToList();
        var bullishTimes = bullish.Select(b => b.Break.Time).ToList();
        var bearishTimes = bearish.Select(b => b.Break.Time).ToList();

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
            if (!Holds(r.Direction, r.To.Time))
                return null;

            // The counter-move lasts until this swing breaks structure, the close beyond its start that continues the trend.
            var swing = r.Confirmed ? byStart.GetValueOrDefault((r.Direction, r.To)) : null;
            var limit = swing?.BreakOfStructure?.Time ?? DateTime.MaxValue;
            var (against, times) = r.Direction == EnumSwingDirection.Down ? (bearish, bearishTimes) : (bullish, bullishTimes);

            // A break of a high or low made after the retracement starts is itself after it, so those before are passed over.
            for (var i = Sawtooth.CountUpTo(times, r.To.Time); i < against.Count && against[i].Break.Time < limit; i++)
            {
                var msb = against[i];
                if (msb.Reference.Time <= r.To.Time)
                    continue;
                var deepest = r.Steps.LastOrDefault(s => s.Point.Time <= msb.Break.Time);
                if (deepest == null || deepest.Percent < minRetracement)
                    continue;
                if (deepest.Percent >= 100)
                    return null;

                var (low, high) = r.Direction == EnumSwingDirection.Down ? (r.To, deepest.Point) : (deepest.Point, r.To);
                var range = new RangeOutline(r.Level, r.Direction, r.From, low, high, msb.Break, deepest.Percent, band, null);
                return range with { End = EndOf(range, IndexOf(msb.Break.Time) + 1) };
            }

            return null;
        }

        PricePoint? EndOf(RangeOutline range, int from)
        {
            if (ends != null && ends.TryGetValue(range, out var read))
            {
                if (read.End != null)
                    return read.End;
                from = Math.Max(from, read.Read + 1);
            }

            PricePoint? ended = null;
            for (var i = from; i < prices.Count && ended == null; i++)
            {
                var high = basis == EnumPriceBasis.Wick ? prices[i].High : prices[i].Close;
                var low = basis == EnumPriceBasis.Wick ? prices[i].Low : prices[i].Close;
                if (high > range.BandHigh)
                    ended = new PricePoint(prices[i].DateTime, high);
                else if (low < range.BandLow)
                    ended = new PricePoint(prices[i].DateTime, low);
            }

            if (ends != null)
                ends[range] = (prices.Count - 1, ended);
            return ended;
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
