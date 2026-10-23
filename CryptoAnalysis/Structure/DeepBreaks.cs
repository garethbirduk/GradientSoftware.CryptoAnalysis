using System.Runtime.CompilerServices;

namespace Gradient.CryptoAnalysis;

/// <summary>
/// A market structure break that goes further back than the last swing, by its candles. After a run of swings one way at
/// a level, inside one leg, the counter-move closes beyond the turn (the bounce high of a downswing, the pullback low of
/// an upswing) of the last swing: that is the market structure break the structure already has. When it goes on to close
/// beyond the turns of the swings before that one too, each further swing it takes back is one of these. Back is how many
/// of the run's last swings have their turns closed beyond, two or more, and Of how many swings the run has. Reference is
/// the candle of the turn that had to be closed beyond for that, From the candle the earliest of those swings starts at,
/// Far the candle the counter-move began from, and Break the candle of the close. Level is the level the run lay at then,
/// and LastStart and LastBreak name the run's last swing.
/// </summary>
public sealed record DeepBreak(int Level, EnumAnnotationType Type, int Back, int Of, int Reference, int From, int Far, int Break, int LastStart, int LastBreak);

/// <summary>
/// The market structure breaks that go further back than the last swing (see <see cref="DeepBreak"/>), each from what
/// was known at the candle of its close. One is found each time the counter-move takes back more swings than it had, so a
/// counter-move can have several, each further back than the one before. Once found it is never unmade.
/// </summary>
public static class DeepBreaks
{
    // The most swings of a run that are looked back over.
    private const int Furthest = 64;

    private static readonly ConditionalWeakTable<StructureStore, Lazy<List<DeepBreak>>> Found = [];

    /// <summary>
    /// Every one the store's candles have, in the order of their closes; found once a store and kept.
    /// </summary>
    public static IReadOnlyList<DeepBreak> Find(StructureStore store) => Found.GetValue(store, s => new Lazy<List<DeepBreak>>(() => Read(s))).Value;

    /// <summary>
    /// Those whose close lies at candles from to to, as known at the close of a candle: found by then, each at the level
    /// its run's last swing lies at then.
    /// </summary>
    public static List<DeepBreak> Over(StructureStore store, int from, int to, int at)
    {
        var found = new List<DeepBreak>();
        foreach (var deep in Find(store))
        {
            if (deep.Break > Math.Min(to, at))
                break;
            if (deep.Break < from)
                continue;
            var last = store.Swings(deep.LastBreak, deep.LastBreak, at).FirstOrDefault(x => x.Start == deep.LastStart && x.Break == deep.LastBreak);
            found.Add(last != null ? deep with { Level = last.Level } : deep);
        }

        return found;
    }

    // A run of swings one way that a candidate swing follows: the swings, the latest first, and for each how far the price
    // must close to be beyond its turn and those of every swing after it.
    private sealed class Run(List<StoredSwing> swings, double[] beyond)
    {
        public List<StoredSwing> Swings => swings;
        public double[] Beyond => beyond;
        public int Back { get; set; }
    }

    private static List<DeepBreak> Read(StructureStore store)
    {
        var found = new List<DeepBreak>();
        var runs = new Dictionary<(int Start, EnumSwingDirection Direction, int Level), Run?>();
        for (var t = 0; t < store.Candles; t++)
        {
            foreach (var candidate in store.Candidates(t))
            {
                // The counter-move is read where its furthest point moves: this candle's close.
                if (candidate.Extreme != t || candidate.Start >= t)
                    continue;
                var key = (candidate.Start, candidate.Direction, candidate.Level);
                if (!runs.TryGetValue(key, out var run))
                    runs[key] = run = RunBefore(store, candidate, t);
                if (run == null)
                    continue;

                var down = candidate.Direction == EnumSwingDirection.Down;
                var close = store.Price(t, high: down);
                var back = 0;
                while (back < run.Beyond.Length && (down ? close > run.Beyond[back] : close < run.Beyond[back]))
                    back++;
                if (back <= run.Back)
                    continue;
                run.Back = back;
                if (back < 2)
                    continue;

                // The turn that had to be closed beyond: the furthest of those of the swings taken back.
                var taken = run.Swings.Take(back).ToList();
                var reference = down ? taken.MaxBy(x => store.Price(x.Extreme, true))! : taken.MinBy(x => store.Price(x.Extreme, false))!;
                found.Add(new DeepBreak(candidate.Level, down ? EnumAnnotationType.BullishMarketStructureBreak : EnumAnnotationType.BearishMarketStructureBreak,
                    back, run.Swings.Count, reference.Extreme, taken[^1].Start, candidate.Start, t, run.Swings[0].Start, run.Swings[0].Break));
            }
        }

        return found;
    }

    // The run a candidate swing follows, as known at a candle: the confirmed swings of its level and direction that broke
    // structure one after another inside one leg, the last of them just before the candidate starts. Null when it follows
    // fewer than two.
    private static Run? RunBefore(StructureStore store, StoredCandidate candidate, int t)
    {
        StoredSwing? last = null;
        foreach (var back in new[] { 64, 512, 4096 })
        {
            last = store.Swings(Math.Max(0, candidate.Start - back), candidate.Start, t)
                .Where(x => x.Level == candidate.Level && x.Direction == candidate.Direction && x.Break <= candidate.Start).MaxBy(x => x.Break);
            if (last != null || back > candidate.Start)
                break;
        }

        // The candidate starts in the leg the swing is in when no point of a coarser level lies between them.
        if (last == null || (candidate.Start - 1 > last.Break && store.Pivots(last.Break + 1, candidate.Start - 1, t, candidate.Level - 1).Count > 0))
            return null;

        var run = new List<StoredSwing>();
        foreach (var back in new[] { 256, 2048, 16384, int.MaxValue })
        {
            var from = back > candidate.Start - last.Leg ? last.Leg : candidate.Start - back;
            var inLeg = store.Swings(from, candidate.Start, t).Where(x => x.Level == last.Level && x.Leg == last.Leg && x.Break <= last.Break).OrderByDescending(x => x.Break).ToList();
            run = inLeg.TakeWhile(x => x.Direction == last.Direction).Take(Furthest).ToList();
            // Read far enough once the run is ended by a swing the other way, reaches the leg's start, or is as long as is looked at.
            if (run.Count < inLeg.Count || from == last.Leg || run.Count == Furthest)
                break;
        }

        if (run.Count < 2)
            return null;
        var down = last.Direction == EnumSwingDirection.Down;
        var beyond = new double[run.Count];
        for (var i = 0; i < run.Count; i++)
        {
            var turn = store.Price(run[i].Extreme, high: down);
            beyond[i] = i == 0 ? turn : down ? Math.Max(beyond[i - 1], turn) : Math.Min(beyond[i - 1], turn);
        }

        return new Run(run, beyond);
    }
}
