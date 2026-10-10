using System.Text.Json;
using Gradient.CryptoAnalysis.Site;

namespace Gradient.CryptoAnalysis.Test.Fetching;

/// <summary>
/// Checks the keeping of fetched candles: a source's answers are kept as given and converted to the one format candles are
/// read in, and a market reads what is fetched after its own files. Nothing here asks a source for anything.
/// </summary>
[TestClass]
public class DataStoreTests
{
    private static readonly DataSource Source = new("test", "Test", "https://example.test/{pair}?g={seconds}&start={start}&end={end}", 300, "arrays",
        ["time", "low", "high", "open", "close", "volume"], new Dictionary<string, string> { ["x-test"] = "X-USD" });

    private string root = "";

    [TestInitialize]
    public void MakeRoot()
    {
        root = Path.Combine(Path.GetTempPath(), $"DataStoreTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void RemoveRoot()
    {
        Directory.Delete(root, recursive: true);
    }

    private static long Seconds(int year, int month, int day, int hour) =>
        new DateTimeOffset(year, month, day, hour, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    // A source's answer of candles, newest first as a source can give them: each [time, low, high, open, close, volume].
    private static string Answer(params (long Time, double Close)[] candles) =>
        JsonSerializer.Serialize(candles.OrderByDescending(x => x.Time).Select(x => new object[] { x.Time, x.Close - 2, x.Close + 2, x.Close - 1, x.Close, 5.5 }));

    private void KeepRaw(int year, int month, params string[] answers)
    {
        var path = DataStore.RawPath(root, "x-test", Source.Id, year, month);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(answers));
    }

    [TestMethod]
    public void Requests_CoverAMonthAtTheMostCandlesARequestGives()
    {
        var urls = DataStore.Requests(Source, "X-USD", 2024, 9, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        CollectionAssert.AreEqual(new[]
        {
            "https://example.test/X-USD?g=3600&start=2024-09-01T00:00:00Z&end=2024-09-13T11:00:00Z",
            "https://example.test/X-USD?g=3600&start=2024-09-13T12:00:00Z&end=2024-09-25T23:00:00Z",
            "https://example.test/X-USD?g=3600&start=2024-09-26T00:00:00Z&end=2024-09-30T23:00:00Z",
        }, urls);
    }

    [TestMethod]
    public void Requests_OfAMonthStillGoingAskOnlyForTheHoursThatHaveBegun()
    {
        var urls = DataStore.Requests(Source, "X-USD", 2024, 9, new DateTime(2024, 9, 14, 6, 30, 0, DateTimeKind.Utc));

        CollectionAssert.AreEqual(new[]
        {
            "https://example.test/X-USD?g=3600&start=2024-09-01T00:00:00Z&end=2024-09-13T11:00:00Z",
            "https://example.test/X-USD?g=3600&start=2024-09-13T12:00:00Z&end=2024-09-14T06:00:00Z",
        }, urls);
    }

    [TestMethod]
    public void Convert_ReadsTheFieldsInTheSourcesOrderAndKeepsTheMonthsEndedHours()
    {
        KeepRaw(2024, 9,
            Answer((Seconds(2024, 9, 1, 1), 101), (Seconds(2024, 9, 1, 0), 100), (Seconds(2024, 8, 31, 23), 99)),
            Answer((Seconds(2024, 9, 1, 1), 101), (Seconds(2024, 9, 1, 2), 102), (Seconds(2024, 9, 1, 3), 103)));

        // At 03:30 the hour from 03:00 has not ended, so its candle is still being made.
        var kept = DataStore.Convert(root, Source, "x-test", 2024, 9, new DateTime(2024, 9, 1, 3, 30, 0, DateTimeKind.Utc));

        Assert.AreEqual("00:00 100, 01:00 101, 02:00 102", string.Join(", ", kept.Select(x => $"{x.DateTime:HH:mm} {x.Close}")));
        Assert.AreEqual((99.0, 102.0, 98.0, 100.0), (kept[0].Open, kept[0].High, kept[0].Low, kept[0].Close));
        Assert.AreEqual("time,open,high,low,close", File.ReadLines(DataStore.ChunkPath(root, "x-test", Source.Id, 2024, 9)).First());
        CollectionAssert.AreEqual(new[] { (2024, 9, "test", 3, true) }, DataStore.Held(root, "x-test"));
    }

    [TestMethod]
    public void Empty_ListsTheMonthsASourceWasAskedForAndHadNothingIn()
    {
        KeepRaw(2014, 12, "[]");
        KeepRaw(2015, 1, Answer((Seconds(2015, 1, 20, 0), 100)));
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DataStore.Convert(root, Source, "x-test", 2014, 12, now);
        DataStore.Convert(root, Source, "x-test", 2015, 1, now);

        CollectionAssert.AreEqual(new[] { (2014, 12, "test") }, DataStore.Empty(root, "x-test"));
        CollectionAssert.AreEqual(new[] { (2015, 1, "test", 1, true) }, DataStore.Held(root, "x-test"));
    }

    [TestMethod]
    public void Convert_AFormatWithNoConverterIsRefused()
    {
        KeepRaw(2024, 9, "[]");

        var error = Assert.ThrowsException<InvalidOperationException>(() =>
            DataStore.Convert(root, Source with { Format = "tables" }, "x-test", 2024, 9, DateTime.UtcNow));

        StringAssert.Contains(error.Message, "no converter");
    }

    [TestMethod]
    public void Load_FetchedCandlesFillWhatTheFilesLackAndReplaceTheirLastCandle()
    {
        // The file ends on a candle exported while its hour was still being made, with a close that later changed.
        var file = Path.Combine(root, "own.csv");
        File.WriteAllText(file, "time,open,high,low,close\n2024-09-01T00:00:00Z,1,2,0,1.5\n2024-09-01T01:00:00Z,1,2,0,1.7\n");
        KeepRaw(2024, 9, Answer((Seconds(2024, 9, 1, 0), 100), (Seconds(2024, 9, 1, 1), 101), (Seconds(2024, 9, 1, 2), 102)));
        DataStore.Convert(root, Source, "x-test", 2024, 9, new DateTime(2024, 10, 1, 0, 0, 0, DateTimeKind.Utc));

        var prices = Markets.Load(new Market("x-test-1h", "X", "x-test", 1, [new MarketSource("own.csv")]), root);

        Assert.AreEqual("00:00 1.5, 01:00 101, 02:00 102", string.Join(", ", prices.Select(x => $"{x.DateTime:HH:mm} {x.Close}")));
    }
}
