using System.Runtime.CompilerServices;

namespace Gradient.CryptoAnalysis.Research;

/// <summary>
/// What market structure showed at each candle's close, as a chart of the last Window candles to that one would show it:
/// worked out from those candles alone, so it is what could have been known then. A level is that chart's level, so level 1
/// is the structure of the window as a whole and each level after it finer. Working from a window rather than every candle
/// before keeps the cost of a candle the same however long the series, where the replay's grows with it.
/// </summary>
public static class KnownStructure
{
    public const int Window = 500;
    public const int Levels = 4;

    /// <summary>
    /// What a level can show at a candle: a break of structure up or down on it, a market structure break up or down on it
    /// (bullish or bearish), or an uptrend or downtrend holding.
    /// </summary>
    public enum EnumFlag
    {
        BreakUp,
        BreakDown,
        BullishMarketStructureBreak,
        BearishMarketStructureBreak,
        Uptrend,
        Downtrend,
    }

    private static readonly int Flags = Enum.GetValues<EnumFlag>().Length;

    private static readonly IReadOnlyList<KnownBreak> None = [];

    // Each series' flags and breaks once, kept while the series is.
    private static readonly ConditionalWeakTable<IReadOnlyList<Price>, Lazy<(int[] Masks, IReadOnlyList<KnownBreak>[] Breaks)>> Known = [];

    private static (int[] Masks, IReadOnlyList<KnownBreak>[] Breaks) Of(IReadOnlyList<Price> prices) =>
        Known.GetValue(prices, p => new Lazy<(int[], IReadOnlyList<KnownBreak>[])>(() => Kept(p))).Value;

    /// <summary>
    /// The folder each series' flags and breaks are also kept in, from one run to the next; null to keep them in memory
    /// alone. What is kept for a series is used again while its candles are as they were, and when candles have been added
    /// after them only the new ones are worked out. The folder is for one build of the rules: another build's must not be
    /// read.
    /// </summary>
    public static string? CacheDirectory { get; set; }

    private const int FileFormat = 1;

    // A series' flags and breaks, from the folder they are kept in when that has them, and kept there once worked out.
    private static (int[] Masks, IReadOnlyList<KnownBreak>[] Breaks) Kept(IReadOnlyList<Price> prices)
    {
        if (CacheDirectory is not { } folder || prices.Count == 0)
            return Read(prices, EnumPriceBasis.Close, Window, Levels);

        // A series is known by its first candles, which stay as they are while candles are added after them.
        var path = Path.Combine(folder, $"known-w{Window}-l{Levels}-{PriceFingerprint.Of(prices, Math.Min(prices.Count, Window)):x16}.bin");
        var had = Load(path, prices);
        if (had?.Masks.Length == prices.Count)
            return had.Value;

        var read = Read(prices, EnumPriceBasis.Close, Window, Levels, had);
        Save(path, prices, read);
        return read;
    }

    // What a file has of a series: the flags and breaks of its first candles, when those candles are still as they were.
    private static (int[] Masks, IReadOnlyList<KnownBreak>[] Breaks)? Load(string path, IReadOnlyList<Price> prices)
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

            var masks = new int[count];
            var breaks = new IReadOnlyList<KnownBreak>[count];
            for (var t = 0; t < count; t++)
            {
                masks[t] = reader.ReadInt32();
                var made = reader.ReadByte();
                breaks[t] = made == 0 ? None : Enumerable.Range(0, made).Select(_ => new KnownBreak(reader.ReadByte(), (EnumKnownBreak)reader.ReadByte(),
                    (EnumSwingDirection)reader.ReadByte(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadBoolean())).ToArray();
            }

            return (masks, breaks);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Keeps a series' flags and breaks: written beside the file and moved over it, so a reader never has half of one. A
    // folder that cannot be written to keeps nothing.
    private static void Save(string path, IReadOnlyList<Price> prices, (int[] Masks, IReadOnlyList<KnownBreak>[] Breaks) read)
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
                for (var t = 0; t < prices.Count; t++)
                {
                    writer.Write(read.Masks[t]);
                    writer.Write((byte)read.Breaks[t].Count);
                    foreach (var b in read.Breaks[t])
                    {
                        writer.Write((byte)b.Level);
                        writer.Write((byte)b.Kind);
                        writer.Write((byte)b.Direction);
                        writer.Write(b.From);
                        writer.Write(b.Begins);
                        writer.Write(b.Run);
                        writer.Write(b.Weak);
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
    /// The candles at which a level shows a flag, worked out once a series and kept.
    /// </summary>
    public static bool[] Met(IReadOnlyList<Price> prices, int level, EnumFlag flag)
    {
        var bit = 1 << Bit(level, flag);
        return Of(prices).Masks.Select(x => (x & bit) != 0).ToArray();
    }

    /// <summary>
    /// The breaks each candle's close made, at every level, each with what it was read from (see <see cref="KnownBreak"/>);
    /// worked out once a series and kept.
    /// </summary>
    public static IReadOnlyList<KnownBreak>[] Breaks(IReadOnlyList<Price> prices) => Of(prices).Breaks;

    /// <summary>
    /// Each candle's flags at levels 1 to levels, a bit a level and flag (see Bit), each from the window of candles to it.
    /// </summary>
    public static int[] Masks(IReadOnlyList<Price> prices, EnumPriceBasis basis, int window, int levels) => Read(prices, basis, window, levels).Masks;

    /// <summary>
    /// Each candle's flags, as <see cref="Masks"/> gives them, and the breaks its close made, each from the window of candles
    /// to it.
    /// </summary>
    public static (int[] Masks, IReadOnlyList<KnownBreak>[] Breaks) Read(IReadOnlyList<Price> prices, EnumPriceBasis basis, int window, int levels) =>
        Read(prices, basis, window, levels, null);

    // With what is had of the series' first candles already, only the candles after them are worked out.
    private static (int[] Masks, IReadOnlyList<KnownBreak>[] Breaks) Read(IReadOnlyList<Price> prices, EnumPriceBasis basis, int window, int levels,
        (int[] Masks, IReadOnlyList<KnownBreak>[] Breaks)? had)
    {
        var list = prices as List<Price> ?? prices.ToList();
        var masks = new int[list.Count];
        var breaks = new IReadOnlyList<KnownBreak>[list.Count];
        var done = had?.Masks.Length ?? 0;
        if (had is { } kept)
        {
            Array.Copy(kept.Masks, masks, done);
            Array.Copy(kept.Breaks, breaks, done);
        }

        Parallel.For(done, list.Count, t =>
        {
            var from = Math.Max(0, t + 1 - window);
            (masks[t], breaks[t]) = Seen(list.GetRange(from, t + 1 - from), from, basis, levels);
        });
        return (masks, breaks);
    }

    /// <summary>
    /// The bit of a level's flag in a candle's mask.
    /// </summary>
    public static int Bit(int level, EnumFlag flag) => (level - 1) * Flags + (int)flag;

    // What the last of these candles shows, read from them alone; first is where they begin in the series, which a break's
    // From counts from.
    private static (int Mask, IReadOnlyList<KnownBreak> Breaks) Seen(List<Price> prices, int first, EnumPriceBasis basis, int levels)
    {
        var last = prices[^1].DateTime;
        var sawtooth = Sawtooth.Levels(prices, basis, levels);
        var mask = 0;
        List<KnownBreak>? made = null;
        for (var level = 1; level <= levels && level < sawtooth.Count; level++)
        {
            var swings = Sawtooth.Swings(prices, sawtooth, basis, level);
            var breaks = Sawtooth.MarketStructureBreaks(prices, swings, basis);
            var trends = Sawtooth.Trends(swings, breaks);
            void Set(EnumFlag flag, bool on) => mask |= on ? 1 << Bit(level, flag) : 0;

            Set(EnumFlag.BreakUp, swings.Any(x => x.Direction == EnumSwingDirection.Up && x.BreakOfStructure?.Time == last));
            Set(EnumFlag.BreakDown, swings.Any(x => x.Direction == EnumSwingDirection.Down && x.BreakOfStructure?.Time == last));
            Set(EnumFlag.BullishMarketStructureBreak, breaks.Any(x => x.Type == EnumAnnotationType.BullishMarketStructureBreak && x.Break.Time == last));
            Set(EnumFlag.BearishMarketStructureBreak, breaks.Any(x => x.Type == EnumAnnotationType.BearishMarketStructureBreak && x.Break.Time == last));
            Set(EnumFlag.Uptrend, trends.Any(x => x.Direction == EnumSwingDirection.Up && x.End == null));
            Set(EnumFlag.Downtrend, trends.Any(x => x.Direction == EnumSwingDirection.Down && x.End == null));

            if (!swings.Any(x => x.BreakOfStructure?.Time == last) && !breaks.Any(x => x.Break.Time == last))
                continue;

            int At(DateTime time) => first + prices.FindIndex(x => x.DateTime == time);
            // The Swings in the order they were Confirmed, as a Trend counts them.
            var ordered = swings.Where(x => x.BreakOfStructure != null).OrderBy(x => x.BreakOfStructure!.Time).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var swing = ordered[i];
                if (swing.BreakOfStructure!.Time != last)
                    continue;
                var run = 1;
                while (i - run >= 0 && ordered[i - run].Direction == swing.Direction)
                    run++;
                var against = swing.Direction == EnumSwingDirection.Up ? EnumAnnotationType.BearishMarketStructureBreak : EnumAnnotationType.BullishMarketStructureBreak;
                var weak = breaks.Any(b => b.Type == against && b.Break.Time > swing.Start.Time && b.Break.Time < last);
                (made ??= []).Add(new KnownBreak(level, EnumKnownBreak.BreakOfStructure, swing.Direction, At(swing.Start.Time),
                    At(ordered[i - run + 1].Start.Time), run, weak));
            }

            foreach (var b in breaks.Where(x => x.Break.Time == last))
            {
                var direction = b.Type == EnumAnnotationType.BullishMarketStructureBreak ? EnumSwingDirection.Up : EnumSwingDirection.Down;
                var from = At(b.Reference.Time);
                (made ??= []).Add(new KnownBreak(level, EnumKnownBreak.MarketStructureBreak, direction, from, from));
            }
        }

        return (mask, made ?? None);
    }
}

public enum EnumKnownBreak
{
    BreakOfStructure,
    MarketStructureBreak,
}

/// <summary>
/// A break a level shows on a candle's close, with what it was read from. A break of structure Confirms a Swing: Direction
/// is the Swing's, From the candle the Swing begins at, Run how many Swings in a row that way it makes, as the chart of the
/// window counts them (two or more are a Trend), Begins the candle the first of those begins at, and Weak whether the Swing
/// has a market structure break inside it. A market structure break has the direction it breaks in, up for a bullish one,
/// and From and Begins are the candle of the Swing Low or Swing High it closes beyond.
/// </summary>
public sealed record KnownBreak(int Level, EnumKnownBreak Kind, EnumSwingDirection Direction, int From, int Begins, int Run = 0, bool Weak = false);
