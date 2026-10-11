using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Gradient.CryptoAnalysis.Research;

/// <summary>
/// A Range a level shows holding at a candle's close (see <see cref="Ranges"/>), as its lines: Low and High, with Band how
/// far beyond them, as a percentage of its height, a close ends it. From is the candle its Retracement is measured from,
/// Identified the candle whose close identified it, and Retracement the reading there. The lines are fixed once it is
/// identified, so what holds a Range from then on is the price alone.
/// </summary>
public sealed record KnownRange(int Level, EnumSwingDirection Direction, double Low, double High, double Band, int Retracement, int From, int Identified)
{
    public double Height => High - Low;

    public double BandLow => Low - Height * Band / 100;

    public double BandHigh => High + Height * Band / 100;

    /// <summary>
    /// The top of the buy-low zone: the given share of the height above the low.
    /// </summary>
    public double BuyLowTop(double depth) => Low + Height * depth / 100;

    /// <summary>
    /// The bottom of the sell-high zone: the given share of the height below the high.
    /// </summary>
    public double SellHighBottom(double depth) => High - Height * depth / 100;

    /// <summary>
    /// Whether a close ends the Range: one beyond either band.
    /// </summary>
    public bool EndedBy(double close) => close > BandHigh || close < BandLow;
}

/// <summary>
/// The Ranges holding at each candle's close, as a chart of the last candles to that one would show them: worked out from
/// those candles alone, as <see cref="KnownStructure"/> is, so a level is that chart's.
/// </summary>
public static class KnownRanges
{
    private static readonly IReadOnlyList<KnownRange> None = [];

    // Each series' Ranges once for each pair of settings, kept while the series is.
    private static readonly ConditionalWeakTable<IReadOnlyList<Price>, ConcurrentDictionary<(double, double), Lazy<IReadOnlyList<KnownRange>[]>>> Known = [];

    /// <summary>
    /// The Ranges holding at each candle's close, at every level, for a least Retracement and a band; worked out once a
    /// series and pair of settings and kept.
    /// </summary>
    public static IReadOnlyList<KnownRange>[] Holding(IReadOnlyList<Price> prices, double minRetracement = Ranges.DefaultMinRetracement, double band = Ranges.DefaultBand) =>
        Known.GetValue(prices, _ => new()).GetOrAdd((minRetracement, band), key => new Lazy<IReadOnlyList<KnownRange>[]>(() => Kept(prices, key.Item1, key.Item2))).Value;

    /// <summary>
    /// The folder each series' Ranges are also kept in, from one run to the next, a file for each pair of settings; null to
    /// keep them in memory alone. As with <see cref="KnownStructure.CacheDirectory"/>, what is kept for a series is used
    /// again while its candles are as they were, only the candles added after them are worked out, and the folder is for
    /// one build of the rules.
    /// </summary>
    public static string? CacheDirectory { get; set; }

    private const int FileFormat = 1;

    // A series' Ranges, from the folder they are kept in when that has them, and kept there once worked out.
    private static IReadOnlyList<KnownRange>[] Kept(IReadOnlyList<Price> prices, double minRetracement, double band)
    {
        if (CacheDirectory is not { } folder || prices.Count == 0)
            return Read(prices, EnumPriceBasis.Close, KnownStructure.Window, KnownStructure.Levels, minRetracement, band);

        // A series is known by its first candles, which stay as they are while candles are added after them.
        var number = (double x) => x.ToString("0.####", CultureInfo.InvariantCulture);
        var path = Path.Combine(folder, $"ranges-w{KnownStructure.Window}-l{KnownStructure.Levels}-r{number(minRetracement)}-b{number(band)}-{PriceFingerprint.Of(prices, Math.Min(prices.Count, KnownStructure.Window)):x16}.bin");
        var had = Load(path, prices, band);
        if (had?.Length == prices.Count)
            return had;

        var read = Read(prices, EnumPriceBasis.Close, KnownStructure.Window, KnownStructure.Levels, minRetracement, band, had);
        Save(path, prices, read);
        return read;
    }

    // What a file has of a series: the Ranges of its first candles, when those candles are still as they were.
    private static IReadOnlyList<KnownRange>[]? Load(string path, IReadOnlyList<Price> prices, double band)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            using var reader = new BinaryReader(File.OpenRead(path));
            if (reader.ReadInt32() != FileFormat)
                return null;
            var count = reader.ReadInt32();
            if (count > prices.Count || reader.ReadUInt64() != PriceFingerprint.Of(prices, count))
                return null;

            var holding = new IReadOnlyList<KnownRange>[count];
            for (var t = 0; t < count; t++)
            {
                var held = reader.ReadByte();
                holding[t] = held == 0 ? None : Enumerable.Range(0, held).Select(_ => new KnownRange(reader.ReadByte(), (EnumSwingDirection)reader.ReadByte(),
                    reader.ReadDouble(), reader.ReadDouble(), band, reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32())).ToArray();
            }

            return holding;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Keeps a series' Ranges: written beside the file and moved over it, so a reader never has half of one. A folder that
    // cannot be written to keeps nothing.
    private static void Save(string path, IReadOnlyList<Price> prices, IReadOnlyList<KnownRange>[] holding)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var writing = $"{path}.{Guid.NewGuid():N}.tmp";
            using (var writer = new BinaryWriter(File.Create(writing)))
            {
                writer.Write(FileFormat);
                writer.Write(prices.Count);
                writer.Write(PriceFingerprint.Of(prices, prices.Count));
                foreach (var held in holding)
                {
                    writer.Write((byte)held.Count);
                    foreach (var range in held)
                    {
                        writer.Write((byte)range.Level);
                        writer.Write((byte)range.Direction);
                        writer.Write(range.Low);
                        writer.Write(range.High);
                        writer.Write(range.Retracement);
                        writer.Write(range.From);
                        writer.Write(range.Identified);
                    }
                }
            }

            File.Move(writing, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// The Ranges holding at each candle's close at levels 1 to levels, each from the window of candles to it.
    /// </summary>
    public static IReadOnlyList<KnownRange>[] Read(IReadOnlyList<Price> prices, EnumPriceBasis basis, int window, int levels, double minRetracement, double band) =>
        Read(prices, basis, window, levels, minRetracement, band, null);

    // With what is had of the series' first candles already, only the candles after them are worked out.
    private static IReadOnlyList<KnownRange>[] Read(IReadOnlyList<Price> prices, EnumPriceBasis basis, int window, int levels, double minRetracement, double band,
        IReadOnlyList<KnownRange>[]? had)
    {
        var list = prices as List<Price> ?? prices.ToList();
        var holding = new IReadOnlyList<KnownRange>[list.Count];
        var done = had?.Length ?? 0;
        if (had != null)
            Array.Copy(had, holding, done);
        Parallel.For(done, list.Count, t =>
        {
            var from = Math.Max(0, t + 1 - window);
            holding[t] = Seen(list.GetRange(from, t + 1 - from), from, basis, levels, minRetracement, band);
        });
        return holding;
    }

    // The Ranges the last of these candles shows holding, read from them alone; first is where they begin in the series.
    private static IReadOnlyList<KnownRange> Seen(List<Price> prices, int first, EnumPriceBasis basis, int levels, double minRetracement, double band)
    {
        var last = prices[^1].DateTime;
        // A Range at a level is identified by a market structure break one level finer.
        var sawtooth = Sawtooth.Levels(prices, basis, levels + 1);
        var swings = new List<SwingOutline>?[sawtooth.Count];
        var breaks = new List<MarketStructureBreakOutline>?[sawtooth.Count];
        List<SwingOutline> SwingsAt(int level) => swings[level] ??= Sawtooth.Swings(prices, sawtooth, basis, level).OrderBy(x => x.Start.Time).ToList();
        List<MarketStructureBreakOutline> BreaksAt(int level) => breaks[level] ??= Sawtooth.MarketStructureBreaks(prices, SwingsAt(level), basis);

        List<KnownRange>? held = null;
        for (var level = 1; level <= levels && level + 1 < sawtooth.Count; level++)
        {
            var trends = Sawtooth.Trends(SwingsAt(level), BreaksAt(level));
            if (trends.Count == 0)
                continue;
            var retracements = Sawtooth.Retracements(prices, SwingsAt(level), Sawtooth.Candidates(prices, sawtooth, basis, level), basis);
            var range = Ranges.Find(prices, basis, retracements, SwingsAt(level), trends, BreaksAt(level + 1), minRetracement, band)
                .LastOrDefault(x => x.End == null && x.Identified.Time <= last);
            if (range == null)
                continue;
            int At(DateTime time) => first + prices.FindIndex(x => x.DateTime == time);
            (held ??= []).Add(new KnownRange(level, range.Direction, range.Low.Price, range.High.Price, band, range.Retracement, At(range.From.Time), At(range.Identified.Time)));
        }

        return held ?? None;
    }
}
