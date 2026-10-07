using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis;
using Gradient.CryptoAnalysis.Csv;
using Gradient.CryptoAnalysis.Site;
using System.Text.Json;

// Usage: CryptoAnalysis.Site [examplesRoot] [outDir] [--serve] [--port=5178]
// Defaults: CryptoAnalysis.Test/TestData/Terms and artifacts/site, relative to the repo root.
// Builds the static site; with --serve it then serves it on localhost, with replay batches for the full history.

const string FullHistoryDataset = "btc-1h";
var serve = args.Contains("--serve");
var port = args.Where(x => x.StartsWith("--port=")).Select(x => int.Parse(x["--port=".Length..])).DefaultIfEmpty(5178).First();
args = args.Where(x => !x.StartsWith("--")).ToArray();

var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
var examplesRoot = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(repoRoot, "CryptoAnalysis.Test", "TestData", "Terms"));
var outDir = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(repoRoot, "artifacts", "site"));

var docs = TermDocs.Load();
var terms = Terms.All.Select(term =>
{
    var (summary, remarks) = docs.For(term.Type);
    return new
    {
        term.Type,
        term.Label,
        term.Name,
        term.Category,
        term.Position,
        term.Color,
        term.Symbol,
        Summary = summary,
        Remarks = remarks,
        // The definitions the tour and the analyses cite, which the page shows in place of the summary.
        Definitions = Definitions.Of(term.Type),
    };
}).ToList();

var examples = TermExampleLibrary.Discover(examplesRoot).Select(file =>
{
    var example = file.LoadExample();
    var prices = file.LoadPrices();
    var annotations = TermAnnotations.Annotate(prices, example.CloseType, example.Level);
    var detected = annotations.Where(x => x.Type == example.Term).ToList();

    return new
    {
        file.Id,
        example.Term,
        example.Title,
        example.Description,
        example.Reviewed,
        example.CloseType,
        example.Level,
        Matches = detected.SequenceEqual(example.Expected),
        Expected = example.Expected,
        Annotations = annotations,
        Structure = StructureOf(prices),
    };
}).ToList();

Directory.CreateDirectory(outDir);
var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
foreach (var source in Directory.EnumerateFiles(wwwroot, "*", SearchOption.AllDirectories))
{
    var target = Path.Combine(outDir, Path.GetRelativePath(wwwroot, source));
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    File.Copy(source, target, overwrite: true);
}

object SawtoothExample(string id, string title, string description, List<Price> prices) => new
{
    Id = id,
    Title = title,
    Description = description,
    Level = 1,
    Annotations = TermAnnotations.Annotate(prices, EnumCloseType.Close),
    Expected = Array.Empty<object>(),
    Structure = StructureOf(prices),
};

var fullPath = Path.Combine(repoRoot, "CryptoAnalysis.Test", "TestData", "PricesExtensionsData", "COINBASE_BTCUSD, 60", "COINBASE_BTCUSD, 60.csv");
var full = File.Exists(fullPath)
    ? new CsvReaderHelper().ReadData<Price, PriceClassMap>(fullPath).ToList()
    : [];
// The full history is not built into the page: the Replay page loads it from the local server.
var sawtoothExamples = new List<object>();

foreach (var (id, title, description) in new[]
{
    ("BreakOfStructure/01-upswing-single-break", "Snippet: rally then pullback",
        "Level 0 is two lines: the first price (19,952) up to the 20,861 high at 01:00, then the incomplete leg to the latest close. Level 1 adds the structure inside: the 08:00 high and the 10:00 dip before the climb, the lows and highs of the climb, then the lower lows of the incomplete leg."),
    ("HigherHigh/01-uptrend", "Snippet: ends at its high",
        "The snippet closes at its highest price, so there is no incomplete leg: level 0 is a single line from the first price to the high. Each higher level adds the dips and pullbacks inside the climb."),
    ("LowerLow/04-after-opening-rally", "Snippet: opening rally, long decline",
        "The high comes early, so almost the whole snippet is the incomplete leg after it. Level 1 splits that leg into lower lows and the bounces between them."),
})
{
    var file = TermExampleLibrary.Discover(examplesRoot).FirstOrDefault(x => x.Id == id);
    if (file != null)
        sawtoothExamples.Add(SawtoothExample($"Sawtooth/{id}", title, description, file.LoadPrices()));
}

var structure = new
{
    Summary = docs.SummaryOf(typeof(Sawtooth)),
    Examples = sawtoothExamples,
};

var interimRoot = Path.GetFullPath(Path.Combine(examplesRoot, "..", "Structure", "InterimSwings"));
var interimExamples = TermExampleLibrary.Discover(interimRoot).Select(file =>
{
    var example = file.LoadSidecar<InterimSwingExample>();
    var prices = file.LoadPrices();
    var detected = InterimSwings.Detect(prices, example);

    return new
    {
        Id = $"InterimSwings/{file.Id}",
        example.Title,
        example.Description,
        example.Reviewed,
        example.CloseType,
        example.Level,
        example.Depth,
        Matches = detected.SequenceEqual(example.Expected),
        Expected = example.Expected,
        Detected = detected,
        Annotations = TermAnnotations.Annotate(prices, example.CloseType, example.Level),
        Structure = StructureOf(prices),
    };
}).ToList();

var interims = new
{
    Summary = docs.SummaryOfMethod(typeof(Sawtooth), nameof(Sawtooth.Interims)),
    Examples = interimExamples,
};

var data = new { Generated = DateTime.UtcNow, Terms = terms, Examples = examples, Structure = structure, Interims = interims };
var json = JsonSerializer.Serialize(data, new JsonSerializerOptions(TermAnnotations.JsonOptions) { WriteIndented = false });
File.WriteAllText(Path.Combine(outDir, "data.js"), $"window.SITE_DATA = {json};\n");

var mismatched = examples.Count(x => !x.Matches);
Console.WriteLine($"{terms.Count} terms, {examples.Count} examples ({mismatched} mismatched) -> {Path.Combine(outDir, "index.html")}");

if (serve && full.Count > 0)
{
    var sourceDir = Path.Combine(repoRoot, "CryptoAnalysis.Site", "wwwroot");
    var video = new TourVideo(Path.Combine(repoRoot, "tools", "tour-video"), Path.Combine(repoRoot, "artifacts", "tour-video", "tour.mp4"),
        Path.Combine(sourceDir, "tour.json"), $"http://localhost:{port}");
    // The tour's own dataset first, then the others the tour's scenes can play on, such as the longer history from 2020.
    var datasets = new List<Dataset> { new(FullHistoryDataset, Tours.Datasets[0].Name, full) };
    foreach (var dataset in Tours.Datasets.Skip(1))
    {
        if (File.Exists(Path.Combine(repoRoot, dataset.Path)))
            datasets.Add(new Dataset(dataset.Id, dataset.Name, Tours.Load(dataset, repoRoot)));
    }
    await ReplayServer.Run(outDir, datasets, port, sourceDir, video);
}
return 0;

// The sawtooth levels, each level's swings (with the indexes of their interims in the next level's list) and each level's
// market structure breaks, plus the candles, for the page to draw at whichever level is shown.
static object StructureOf(List<Price> prices)
{
    var levels = Sawtooth.Levels(prices, EnumPriceBasis.Close, maxLevel: 8);
    var swings = Enumerable.Range(0, levels.Count)
        .Select(l => Sawtooth.Swings(prices, levels, EnumPriceBasis.Close, l).OrderBy(x => x.Start.Time).ToList())
        .ToList();
    var breaks = swings.Select(list => Sawtooth.MarketStructureBreaks(prices, list, EnumPriceBasis.Close)).ToList();
    // Replaying candle by candle is quadratic, so the page carries replays of up to about a week of hourly candles; longer
    // ones come from the local server.
    const int liveReplayLimit = 200;

    return new
    {
        Sawtooth = levels.Select(level => level.Pivots.Select(p => new
        {
            p.Kind,
            p.Time,
            p.Price,
            ConfirmedTime = p.ConfirmedIndex is int c ? prices[c].DateTime : (DateTime?)null,
        })),
        SwingsByLevel = swings.Select((list, l) => list.Select(w => new
        {
            w.Level,
            w.Direction,
            w.Start,
            w.End,
            w.Extreme,
            w.BreakOfStructure,
            w.Confirmed,
            Interims = l + 1 < swings.Count ? Sawtooth.Interims(w, swings[l + 1]).Select(x => swings[l + 1].IndexOf(x)).ToList() : [],
        })),
        PointsByLevel = levels.Select(level => Sawtooth.Points(prices, level, EnumPriceBasis.Close)),
        MarketStructureBreaksByLevel = breaks,
        TrendsByLevel = swings.Select((list, l) => Sawtooth.Trends(list, breaks[l])),
        RetracementsByLevel = swings.Select((list, l) => Sawtooth.Retracements(prices, list, Sawtooth.Candidates(prices, levels, EnumPriceBasis.Close, l), EnumPriceBasis.Close)),
        RangesByLevel = swings.Select((list, l) => l + 1 < swings.Count
            ? Ranges.Find(prices, EnumPriceBasis.Close, Sawtooth.Retracements(prices, list, Sawtooth.Candidates(prices, levels, EnumPriceBasis.Close, l), EnumPriceBasis.Close), list, Sawtooth.Trends(list, breaks[l]), breaks[l + 1])
            : []),
        LiveByLevel = prices.Count <= liveReplayLimit
            ? Enumerable.Range(0, levels.Count).Select(l => l == 0 ? [] : MarketStructure.Replay(prices, EnumPriceBasis.Close, l))
            : null,
        Replay = prices.Count <= liveReplayLimit ? MarketStructure.Timeline(prices, EnumPriceBasis.Close, maxLevel: 8) : null,
        CandleRuns = CandleRuns.Runs(prices, minLength: 2),
        Prices = new
        {
            T = prices.Select(p => p.DateTime),
            O = prices.Select(p => p.Open),
            H = prices.Select(p => p.High),
            L = prices.Select(p => p.Low),
            C = prices.Select(p => p.Close),
        },
    };
}

static string FindRepoRoot(string start)
{
    for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "CryptoAnalysis.sln")))
            return dir.FullName;
    }

    return Directory.GetCurrentDirectory();
}
