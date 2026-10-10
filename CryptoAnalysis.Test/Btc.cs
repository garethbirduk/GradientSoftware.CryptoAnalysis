using Gradient.CryptoAnalysis.Site;

namespace Gradient.CryptoAnalysis.Test;

/// <summary>
/// The one source of BTC candles (see <see cref="Markets.Btc"/>), for the tests that read real candles: Coinbase's BTC/USD
/// by the hour, as fetched and kept under Data/fetched. More candles are added to it as time goes on, so a test reads a
/// stretch of it with a fixed end, and what it finds there does not change.
/// </summary>
internal static class Btc
{
    /// <summary>
    /// The repository's root, which the candles are kept under.
    /// </summary>
    public static readonly string RepoRoot = FindRepoRoot(AppContext.BaseDirectory);

    private static readonly Lazy<List<Price>> All = new(() => Markets.Load(Markets.All.Single(x => x.Id == Markets.Btc), RepoRoot));

    /// <summary>
    /// Every candle held, from Coinbase's first in July 2015 to the last fetched.
    /// </summary>
    public static List<Price> Hourly => All.Value;

    /// <summary>
    /// The candles from a time up to but not including another.
    /// </summary>
    public static List<Price> Between(DateTime from, DateTime to) => Hourly.Where(x => x.DateTime >= from && x.DateTime < to).ToList();

    /// <summary>
    /// The candles from the start of 2023 to the afternoon of 5 August 2024: the stretch the tour begins in, and the one a
    /// test of an analysis reads, as the structure at a candle depends on where the candles before it begin.
    /// </summary>
    public static List<Price> From2023() => Between(Utc(2023, 1, 1), Utc(2024, 8, 5, 15));

    /// <summary>
    /// The candles from the start of 2020 to the evening of 23 July 2024.
    /// </summary>
    public static List<Price> From2020() => Between(Utc(2020, 1, 1), Utc(2024, 7, 23, 22));

    private static DateTime Utc(int year, int month, int day, int hour = 0) => new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    private static string FindRepoRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CryptoAnalysis.sln")))
                return dir.FullName;
        }

        throw new InvalidOperationException("The repository root (CryptoAnalysis.sln) was not found.");
    }
}
