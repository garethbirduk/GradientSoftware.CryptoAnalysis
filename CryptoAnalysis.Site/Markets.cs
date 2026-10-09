using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gradient.CryptoAnalysis.Site;

/// <summary>
/// A file a market's candles come from: its candles from From up to but not including To. With LondonTime, the file's times
/// are London's clock time though written as UTC, as an export can be, so an hour ahead of UTC in British Summer Time; they
/// are read back to UTC.
/// </summary>
public sealed record MarketSource(string Path, DateTime? From = null, DateTime? To = null, bool LondonTime = false);

/// <summary>
/// A market the Strategies page tests on: an instrument on an exchange at a candle length, its candles from one or more files
/// joined in time. Group is the instrument on its exchange, so the same market at another candle length shares the roles of
/// its periods.
/// </summary>
public sealed record Market(string Id, string Name, string Group, int Hours, IReadOnlyList<MarketSource> Sources);

/// <summary>
/// What a period of a market may be used for. A strategy is searched for on Search periods, as sweeps do; checked on Test
/// periods, each run there counted; and Locked periods are kept back for a final check, nothing run on them until unlocked.
/// </summary>
public enum EnumPeriodRole
{
    Search,
    Test,
    Locked,
}

/// <summary>
/// The markets the local server serves for strategies, each also a dataset of the server by its id.
/// </summary>
public static class Markets
{
    private static readonly DateTime Y2023 = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    // Coinbase's BTC from 2020 is a file of London times up to 2023, then one of UTC times on, as each was exported.
    private static readonly MarketSource[] BtcCoinbase =
    [
        new(System.IO.Path.Combine("CryptoAnalysis.Test", "TestData", "COINBASE_BTCUSD, 60.csv"), To: Y2023, LondonTime: true),
        new(System.IO.Path.Combine("CryptoAnalysis.Test", "TestData", "PricesExtensionsData", "COINBASE_BTCUSD, 60", "COINBASE_BTCUSD, 60.csv"), From: Y2023),
    ];

    public static readonly IReadOnlyList<Market> All =
    [
        new("btc-coinbase-1h", "BTC/USD · Coinbase · 1h", "btc-coinbase", 1, BtcCoinbase),
        new("btc-coinbase-4h", "BTC/USD · Coinbase · 4h", "btc-coinbase", 4, BtcCoinbase),
        new("eth-coinbase-1h", "ETH/USD · Coinbase · 1h", "eth-coinbase", 1, [new(System.IO.Path.Combine("Data", "COINBASE_ETHUSD, 60", "COINBASE_ETHUSD, 60.csv"))]),
        new("btc-bitstamp-1h", "BTC/USD · Bitstamp · 1h", "btc-bitstamp", 1, [new(System.IO.Path.Combine("Data", "BITSTAMP_BTCUSD, 60", "BITSTAMP_BTCUSD, 60 (1).csv"))]),
    ];

    /// <summary>
    /// The tour's datasets that hold a market group's candles, so a run on one of them keeps to that group's roles.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> DatasetGroups = new Dictionary<string, string>
    {
        ["btc-1h"] = "btc-coinbase",
        ["btc-1h-2020"] = "btc-coinbase",
        ["btc-4h-2020"] = "btc-coinbase",
    };

    /// <summary>
    /// The market group whose roles a dataset keeps to; null for one of no group, which has none.
    /// </summary>
    public static string? GroupOf(string dataset) => All.FirstOrDefault(x => x.Id == dataset)?.Group ?? DatasetGroups.GetValueOrDefault(dataset);

    /// <summary>
    /// Loads a market: its sources' candles in time order, the first of any time kept, built to its candle length when that
    /// is more than an hour.
    /// </summary>
    public static List<Price> Load(Market market, string repoRoot)
    {
        var prices = market.Sources.SelectMany(source =>
            new Csv.CsvReaderHelper().ReadData<Price, global::CryptoAnalysis.Csv.ClassMaps.PriceClassMap>(System.IO.Path.Combine(repoRoot, source.Path))
                .Select(x =>
                {
                    if (source.LondonTime)
                        x.DateTime = FromLondon(x.DateTime);
                    return x;
                })
                .Where(x => (source.From == null || x.DateTime >= source.From) && (source.To == null || x.DateTime < source.To)))
            .DistinctBy(x => x.DateTime)
            .OrderBy(x => x.DateTime)
            .ToList();
        return market.Hours > 1 ? Resample.To(prices, TimeSpan.FromHours(market.Hours)) : prices;
    }

    /// <summary>
    /// A London clock time, written as UTC, as the UTC time it was. The hour the clocks go back is read as the later of the
    /// two it could be.
    /// </summary>
    public static DateTime FromLondon(DateTime time) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(time, DateTimeKind.Unspecified), London);

    /// <summary>
    /// The years the candles of a window of prices fall in, first up to but not including end.
    /// </summary>
    public static IEnumerable<int> Years(IReadOnlyList<Price> prices, (int First, int End) window) =>
        window.End > window.First ? Enumerable.Range(prices[window.First].DateTime.Year, prices[window.End - 1].DateTime.Year - prices[window.First].DateTime.Year + 1) : [];
}

/// <summary>
/// The roles of the periods, years, of each market group, kept in periods.json, and the log of what has been done with
/// them, kept apart so it is not edited with the roles: each change of role, and each run on a Test period. A period with no
/// role kept is Search.
/// </summary>
public sealed class PeriodRoles(string path, string logPath)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    // The log is a line of JSON an entry.
    private static readonly JsonSerializerOptions LogOptions = new() { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private readonly object gate = new();

    /// <summary>
    /// The roles kept, by group and then year.
    /// </summary>
    public Dictionary<string, Dictionary<int, EnumPeriodRole>> All()
    {
        lock (gate)
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, Dictionary<int, EnumPeriodRole>>>(File.ReadAllText(path), JsonOptions) ?? []
                : [];
        }
    }

    /// <summary>
    /// The role of a year of a group.
    /// </summary>
    public EnumPeriodRole Role(string group, int year) =>
        All().GetValueOrDefault(group)?.GetValueOrDefault(year, EnumPeriodRole.Search) ?? EnumPeriodRole.Search;

    /// <summary>
    /// Sets the role of a year of a group, and logs the change.
    /// </summary>
    public void Set(string group, int year, EnumPeriodRole role)
    {
        lock (gate)
        {
            var roles = All();
            var was = roles.GetValueOrDefault(group)?.GetValueOrDefault(year, EnumPeriodRole.Search) ?? EnumPeriodRole.Search;
            if (!roles.TryGetValue(group, out var years))
                roles[group] = years = [];
            years[year] = role;
            File.WriteAllText(path, JsonSerializer.Serialize(roles, JsonOptions));
            Log(new { Time = DateTime.UtcNow, Kind = "role", Group = group, Year = year, From = was, To = role });
        }
    }

    /// <summary>
    /// What stops a run on a window of a dataset's prices, or null when it may run: nothing runs on a Locked year, and a
    /// sweep, which searches, runs only on Search years. A run that may go ahead on a Test year is logged.
    /// </summary>
    public string? Check(string dataset, IReadOnlyList<Price> prices, (int First, int End) window, string kind, string strategy)
    {
        if (Markets.GroupOf(dataset) is not { } group)
            return null;
        var years = Markets.Years(prices, window).Select(year => (Year: year, Role: Role(group, year))).ToList();
        if (years.FirstOrDefault(x => x.Role == EnumPeriodRole.Locked) is { Year: > 0 } locked)
            return $"{locked.Year} of {group} is Locked, kept back for a final check. Unlock it on the Strategies page to run on it.";
        if (kind == "sweep" && years.FirstOrDefault(x => x.Role != EnumPeriodRole.Search) is { Year: > 0 } test)
            return $"Sweeps search, so they run only on Search periods, and {test.Year} of {group} is {test.Role}.";

        var tested = years.Where(x => x.Role == EnumPeriodRole.Test).Select(x => x.Year).ToList();
        if (tested.Count > 0)
        {
            lock (gate)
                Log(new { Time = DateTime.UtcNow, Kind = kind, Group = group, Years = tested, Dataset = dataset, Strategy = strategy,
                    From = prices[window.First].DateTime, To = prices[window.End - 1].DateTime });
        }

        return null;
    }

    /// <summary>
    /// How many baselines have been run on each Test year of each group, as the log has them: each a look at the period.
    /// </summary>
    public Dictionary<(string Group, int Year), int> TestRuns()
    {
        var counts = new Dictionary<(string, int), int>();
        if (!File.Exists(logPath))
            return counts;
        lock (gate)
        {
            foreach (var line in File.ReadLines(logPath))
            {
                if (JsonSerializer.Deserialize<JsonElement>(line) is not { ValueKind: JsonValueKind.Object } entry
                    || entry.GetProperty("Kind").GetString() != "baseline")
                    continue;
                var group = entry.GetProperty("Group").GetString() ?? "";
                foreach (var year in entry.GetProperty("Years").EnumerateArray())
                    counts[(group, year.GetInt32())] = counts.GetValueOrDefault((group, year.GetInt32())) + 1;
            }
        }

        return counts;
    }

    private void Log(object entry)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        File.AppendAllText(logPath, JsonSerializer.Serialize(entry, LogOptions) + "\n");
    }
}
