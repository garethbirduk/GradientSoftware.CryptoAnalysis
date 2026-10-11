namespace Gradient.CryptoAnalysis;

public static partial class MarketStructure
{
    /// <summary>
    /// Takes what a replay tracks at each candle without reading every candle again: the candles are read once, one at a
    /// time (see <see cref="SawtoothReader"/>), and what is worked out of a swing is kept with it while it holds. At a
    /// candle, each level's swings are gone through, but a swing's outline, its market structure break and its retracement
    /// are only made when they are new or have changed, and only the end of each level's pivots is listed. What it takes
    /// is what <see cref="TakeSnapshot"/> does from the candles to there.
    /// </summary>
    private sealed class Replayer(EnumPriceBasis basis, int maxLevel, int minTrendSwings, double minRetracement, double band)
    {
        private readonly SawtoothReader _reader = new(basis);
        private readonly Dictionary<DateTime, int> _index = [];
        private readonly Dictionary<RangeOutline, (int Read, PricePoint? End)> _ends = [];

        /// <summary>
        /// Reads the next candle.
        /// </summary>
        public void Add(Price price)
        {
            _index.TryAdd(price.DateTime, _reader.Count);
            _reader.Add(price);
        }

        /// <summary>
        /// Everything the replay tracks at the last candle read, leaving out what ended before the candle keepFrom.
        /// </summary>
        public Snapshot Take(int? keepFrom)
        {
            var prices = _reader.Read;
            var t = prices.Count - 1;
            var kept = keepFrom is int k && k > 0 ? Math.Min(k, t) : 0;
            var keep = kept > 0 ? prices[kept].DateTime : DateTime.MinValue;
            // On wicks a candle can be a high and a low, so a pivot is not always against the one two before it: every pivot is listed.
            var levels = _reader.ReadLevels(maxLevel, basis == EnumPriceBasis.Close ? kept : 0);

            var pivots = new List<LevelPivot>();
            for (var level = 0; level < levels.Count; level++)
            {
                var held = levels[level].Pivots.Where(p => p.Kind != EnumPivotKind.Current).ToList();
                var first = Math.Max(0, held.FindLastIndex(p => p.Time < keep));
                pivots.AddRange(held.Skip(keep == DateTime.MinValue ? 0 : first).Select(p => new LevelPivot(level, p)));
            }

            var top = Math.Min(maxLevel, levels.Count);
            var swingsByLevel = new List<SwingOutline>[top + 1];
            var breaksByLevel = new List<MarketStructureBreakOutline>[top + 1];
            var trendsByLevel = new List<TrendOutline>[top + 1];
            var candidatesByLevel = new List<CandidateSwing>[top + 1];
            var retracementsByLevel = new List<RetracementOutline>[top + 1];
            for (var level = 1; level <= top; level++)
            {
                var read = level < levels.Count ? levels[level].Swings : [];
                swingsByLevel[level] = read.Select(x => _reader.Outline(x, level)).OrderBy(x => x.Start.Time).ToList();
                breaksByLevel[level] = Breaks(read, t);
                trendsByLevel[level] = Sawtooth.Trends(swingsByLevel[level], breaksByLevel[level], minTrendSwings);
                candidatesByLevel[level] = _reader.Candidates(level);
                retracementsByLevel[level] = _reader.Retracements(level, read);
            }

            var swings = new List<SwingOutline>();
            var candidates = new List<CandidateSwing>();
            var breaks = new List<LevelBreak>();
            var trends = new List<TrendOutline>();
            var retracements = new List<RetracementOutline>();
            var ranges = new List<RangeOutline>();
            for (var level = 1; level <= top; level++)
            {
                swings.AddRange(swingsByLevel[level].Where(x => x.End.Time >= keep));
                breaks.AddRange(breaksByLevel[level].Where(x => x.Break.Time >= keep).Select(x => new LevelBreak(level, x)));
                trends.AddRange(trendsByLevel[level].Where(x => x.End == null || x.End.Time >= keep));
                candidates.AddRange(candidatesByLevel[level]);
                retracements.AddRange(retracementsByLevel[level].Where(x => !x.Confirmed || x.Deepest.Point.Time >= keep));
                // A range needs the structure one level finer, so the finest level has none.
                if (level < top)
                {
                    ranges.AddRange(Ranges.Find(prices, basis, retracementsByLevel[level], swingsByLevel[level], trendsByLevel[level],
                        breaksByLevel[level + 1], minRetracement, band, _index, _ends).Where(x => x.End == null || x.End.Time >= keep));
                }
            }

            return new Snapshot(
                pivots,
                levels.SelectMany((l, level) => Sawtooth.Points(prices, new SawtoothLevel(level, l.Pivots), basis, l.Furthest)).Where(x => x.Time >= keep).ToList(),
                CandleRuns.RunsFrom(prices, minLength: 2, kept).Where(x => x.End.Time >= keep).ToList(),
                swings, candidates, breaks, trends, retracements, ranges);
        }

        // A level's market structure breaks, as Sawtooth.MarketStructureBreaks finds them among its swings, which are given
        // in the order they broke structure: each swing protects the pivot against it until the next swing its way breaks
        // structure, and the first close beyond that pivot before then is a break.
        private List<MarketStructureBreakOutline> Breaks(List<SawtoothReader.Swing> swings, int last)
        {
            var breaks = new List<MarketStructureBreakOutline>();
            SawtoothReader.Swing? up = null, down = null;
            foreach (var swing in swings)
            {
                ref var before = ref swing.Up ? ref up : ref down;
                if (before != null && Beyond(before, swing.Break - 1) is { } broken)
                    breaks.Add(broken);
                before = swing;
            }

            if (up != null && Beyond(up, last) is { } low)
                breaks.Add(low);
            if (down != null && Beyond(down, last) is { } high)
                breaks.Add(high);
            return breaks.OrderBy(x => x.Break.Time).ThenBy(x => x.Type).ToList();
        }

        // The first close beyond the pivot against a swing, after its break of structure and up to a candle: looked for on
        // from where it was last looked for to, and kept once found.
        private MarketStructureBreakOutline? Beyond(SawtoothReader.Swing swing, int to)
        {
            var prices = _reader.Read;
            for (var c = Math.Max(swing.Break, swing.BeyondRead) + 1; swing.BeyondAt < 0 && c <= to; c++)
            {
                var value = swing.Up ? Sawtooth.LowOf(prices[c], basis) : Sawtooth.HighOf(prices[c], basis);
                if (swing.Up ? value < swing.Counter.Price : value > swing.Counter.Price)
                {
                    swing.BeyondAt = c;
                    swing.Beyond = new MarketStructureBreakOutline(
                        swing.Up ? EnumAnnotationType.BearishMarketStructureBreak : EnumAnnotationType.BullishMarketStructureBreak,
                        new PricePoint(swing.Counter.Time, swing.Counter.Price), new PricePoint(prices[c].DateTime, value));
                }
            }

            if (swing.BeyondAt < 0)
                swing.BeyondRead = Math.Max(swing.BeyondRead, to);
            return swing.BeyondAt >= 0 && swing.BeyondAt <= to ? swing.Beyond : null;
        }
    }
}
