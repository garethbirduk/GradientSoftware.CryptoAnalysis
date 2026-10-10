using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Gradient.CryptoAnalysis.Site;

/// <summary>
/// A place hourly candles are fetched from, as data-sources.json describes it: the address of a request, with {pair},
/// {seconds}, {start} and {end} filled in; the most candles a request gives; the format of its answers, which names the
/// converter that reads them (see <see cref="DataStore.Converters"/>), with the order of a candle's fields for a format
/// that needs it; and the pair it calls each market group by.
/// </summary>
public sealed record DataSource(string Id, string Name, string Url, int Most, string Format, IReadOnlyList<string> Fields, IReadOnlyDictionary<string, string> Pairs);

/// <summary>
/// The candles fetched from sources and kept, under Data/fetched, which is local. A month of a market group from a source
/// is kept twice: as the source answered, untouched, in its raw folder; and converted to the one format candles are read
/// in, the CSV the markets' own files have. So whatever a source's format, only its converter knows it, and a month can be
/// converted again without being fetched again. A market's candles are those of its own files and then these, so what is
/// fetched fills what the files lack.
/// </summary>
public static class DataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// The folder the fetched candles are kept in, under the repository's root.
    /// </summary>
    public static readonly string Folder = Path.Combine("Data", "fetched");

    /// <summary>
    /// The converters, by the format a source names: each reads a source's answer to one request as candles, oldest first.
    /// "arrays" is a list of candles, each a list of numbers in the order of the source's fields, its time in seconds.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Func<DataSource, string, List<Price>>> Converters =
        new Dictionary<string, Func<DataSource, string, List<Price>>> { ["arrays"] = Arrays };

    /// <summary>
    /// The sources of data-sources.json in the site's folder; none when there is no such file.
    /// </summary>
    public static IReadOnlyList<DataSource> Sources(string siteDir)
    {
        var path = Path.Combine(siteDir, "data-sources.json");
        if (!File.Exists(path))
            return [];
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        return json.RootElement.GetProperty("sources").Deserialize<List<DataSource>>(JsonOptions) ?? [];
    }

    /// <summary>
    /// The file a month of a group's candles from a source is kept in.
    /// </summary>
    public static string ChunkPath(string repoRoot, string group, string source, int year, int month) =>
        Path.Combine(repoRoot, Folder, group, source, $"{year:0000}-{month:00}.csv");

    /// <summary>
    /// The months of a group that have been fetched, each with the source it is from, how many candles it has, and whether
    /// that is all the source has of it: it was fetched after the month had ended, so hours it lacks are hours the source
    /// lacks, as when an exchange was down. A month fetched from more than one source is listed for each.
    /// </summary>
    public static List<(int Year, int Month, string Source, int Candles, bool Whole)> Held(string repoRoot, string group)
    {
        var held = new List<(int, int, string, int, bool)>();
        var dir = Path.Combine(repoRoot, Folder, group);
        if (!Directory.Exists(dir))
            return held;
        foreach (var file in Directory.EnumerateFiles(dir, "*.csv", SearchOption.AllDirectories).Order())
        {
            var name = Path.GetFileNameWithoutExtension(file).Split('-');
            if (name is not [var year, var month] || !int.TryParse(year, out var y) || !int.TryParse(month, out var m))
                continue;
            var source = Path.GetFileName(Path.GetDirectoryName(file)!);
            var raw = RawPath(repoRoot, group, source, y, m);
            var whole = File.Exists(raw) && File.GetLastWriteTimeUtc(raw) >= new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1).AddHours(1);
            held.Add((y, m, source, Math.Max(0, File.ReadLines(file).Count() - 1), whole));
        }

        return held;
    }

    /// <summary>
    /// The months of a group a source was asked for and had no candles in: its answers are kept, and no candles are. A run
    /// of them before a group's first candle is where the source's history begins.
    /// </summary>
    public static List<(int Year, int Month, string Source)> Empty(string repoRoot, string group)
    {
        var empty = new List<(int, int, string)>();
        var dir = Path.Combine(repoRoot, Folder, group);
        if (!Directory.Exists(dir))
            return empty;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).Order())
        {
            var name = Path.GetFileNameWithoutExtension(file).Split('-');
            var source = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(file)!)!);
            if (name is [var year, var month] && int.TryParse(year, out var y) && int.TryParse(month, out var m) && !File.Exists(ChunkPath(repoRoot, group, source, y, m)))
                empty.Add((y, m, source));
        }

        return empty;
    }

    /// <summary>
    /// Every candle fetched for a group, in the order of its files: source by source, then month by month.
    /// </summary>
    public static List<Price> Read(string repoRoot, string group)
    {
        var dir = Path.Combine(repoRoot, Folder, group);
        if (!Directory.Exists(dir))
            return [];
        return Directory.EnumerateFiles(dir, "*.csv", SearchOption.AllDirectories).Order().SelectMany(ReadChunk).ToList();
    }

    /// <summary>
    /// The file a month of a group's answers from a source is kept in, as the source gave them: a list of the answers, one
    /// for each request, each as text.
    /// </summary>
    public static string RawPath(string repoRoot, string group, string source, int year, int month) =>
        Path.Combine(repoRoot, Folder, group, source, "raw", $"{year:0000}-{month:00}.json");

    /// <summary>
    /// Converts a month of a group's answers from a source, as kept, to candles in the one format they are read in, and
    /// keeps them in place of what was kept of that month from that source. A candle of another month is left out, and so
    /// is one whose hour has not ended at now, as it is still being made. Gives the candles kept.
    /// </summary>
    public static List<Price> Convert(string repoRoot, DataSource source, string group, int year, int month, DateTime now)
    {
        if (!Converters.TryGetValue(source.Format ?? "", out var convert))
            throw new InvalidOperationException($"{source.Name} has the format \"{source.Format}\", which has no converter: {string.Join(", ", Converters.Keys)}.");
        var answers = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(RawPath(repoRoot, group, source.Id, year, month))) ?? [];
        var kept = answers.SelectMany(x => convert(source, x))
            .Where(x => x.DateTime.Year == year && x.DateTime.Month == month && x.DateTime.AddHours(1) <= now)
            .DistinctBy(x => x.DateTime)
            .OrderBy(x => x.DateTime)
            .ToList();
        Write(repoRoot, group, source.Id, year, month, kept);
        return kept;
    }

    // The "arrays" converter: a list of candles, each a list of numbers in the order of the source's fields.
    private static List<Price> Arrays(DataSource source, string json)
    {
        var at = new[] { "time", "open", "high", "low", "close" }.ToDictionary(x => x, x => source.Fields.ToList().IndexOf(x));
        if (at.Values.Any(x => x < 0))
            throw new InvalidOperationException($"The source {source.Id} needs the fields time, open, high, low and close.");
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"{source.Name} did not answer with candles: {json[..Math.Min(json.Length, 200)]}");
        return doc.RootElement.EnumerateArray()
            .Select(x => new Price
            {
                DateTime = DateTimeOffset.FromUnixTimeSeconds(x[at["time"]].GetInt64()).UtcDateTime,
                Open = x[at["open"]].GetDouble(),
                High = x[at["high"]].GetDouble(),
                Low = x[at["low"]].GetDouble(),
                Close = x[at["close"]].GetDouble(),
            })
            .OrderBy(x => x.DateTime)
            .ToList();
    }

    /// <summary>
    /// The requests that fetch a month of hourly candles from a source: the address of each, as many as the month needs at
    /// the most candles a request gives. Of a month still going at now, only the hours that have begun are asked for, as a
    /// source can refuse a time in the future.
    /// </summary>
    public static List<string> Requests(DataSource source, string pair, int year, int month, DateTime now)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var hour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        var end = new[] { start.AddMonths(1), hour.AddHours(1) }.Min();
        var urls = new List<string>();
        for (var from = start; from < end; from = from.AddHours(source.Most))
        {
            var to = new[] { from.AddHours(source.Most - 1), end.AddHours(-1) }.Min();
            urls.Add(source.Url.Replace("{pair}", pair).Replace("{seconds}", "3600")
                .Replace("{start}", Iso(from)).Replace("{end}", Iso(to)));
        }

        return urls;
    }

    /// <summary>
    /// Fetches a month of a group's hourly candles from a source, keeps the answers as given, and converts them (see
    /// <see cref="Convert"/>). Gives the candles kept.
    /// </summary>
    public static async Task<List<Price>> Fetch(HttpClient http, string repoRoot, DataSource source, string group, int year, int month, DateTime now)
    {
        if (!source.Pairs.TryGetValue(group, out var pair))
            throw new InvalidOperationException($"{source.Name} has no pair for {group} in data-sources.json.");
        var answers = new List<string>();
        foreach (var url in Requests(source, pair, year, month, now))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("CryptoAnalysis");
            using var response = await http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"{source.Name} answered {(int)response.StatusCode}: {body[..Math.Min(body.Length, 200)]}");
            answers.Add(body);
        }

        var raw = RawPath(repoRoot, group, source.Id, year, month);
        Directory.CreateDirectory(Path.GetDirectoryName(raw)!);
        File.WriteAllText(raw, JsonSerializer.Serialize(answers));
        return Convert(repoRoot, source, group, year, month, now);
    }

    /// <summary>
    /// Keeps a month of a group's candles from a source, in place of what was kept of it. A month with no candles keeps no file.
    /// </summary>
    public static void Write(string repoRoot, string group, string source, int year, int month, IReadOnlyList<Price> prices)
    {
        var path = ChunkPath(repoRoot, group, source, year, month);
        if (prices.Count == 0)
        {
            if (File.Exists(path))
                File.Delete(path);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var csv = new StringBuilder("time,open,high,low,close\n");
        foreach (var x in prices)
            csv.Append(Iso(x.DateTime)).Append(',').Append(Number(x.Open)).Append(',').Append(Number(x.High)).Append(',')
                .Append(Number(x.Low)).Append(',').Append(Number(x.Close)).Append('\n');
        File.WriteAllText(path, csv.ToString());
    }

    private static IEnumerable<Price> ReadChunk(string path) =>
        new Csv.CsvReaderHelper().ReadData<Price, global::CryptoAnalysis.Csv.ClassMaps.PriceClassMap>(path);

    private static string Iso(DateTime time) => time.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
