using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis;
using Gradient.CryptoAnalysis.Csv;
using Gradient.CryptoAnalysis.Site;
using System.Text.Json;

// Usage: CryptoAnalysis.Site [examplesRoot] [outDir]
// Defaults: CryptoAnalysis.Test/TestData/Terms and artifacts/site, relative to the repo root.

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
        Swings = TermAnnotations.Swings(prices, example.CloseType, example.Level),
        SwingsByLevel = SwingsByLevel(prices),
        MarketStructureBreaks = TermAnnotations.MarketStructureBreaks(prices, example.CloseType),
        CandleRuns = CandleRuns.Runs(prices),
        Sawtooth = Sawtooth.Levels(prices, EnumPriceBasis.Close, maxLevel: 8)
            .Select(level => level.Pivots.Select(p => new { p.Kind, p.Time, p.Price })),
        Prices = new
        {
            T = prices.Select(p => p.DateTime),
            O = prices.Select(p => p.Open),
            H = prices.Select(p => p.High),
            L = prices.Select(p => p.Low),
            C = prices.Select(p => p.Close),
        },
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
    MarketStructureBreaks = TermAnnotations.MarketStructureBreaks(prices, EnumCloseType.Close),
    CandleRuns = CandleRuns.Runs(prices),
    Swings = Array.Empty<object>(),
    SwingsByLevel = SwingsByLevel(prices),
    Expected = Array.Empty<object>(),
    Sawtooth = Sawtooth.Levels(prices, EnumPriceBasis.Close, maxLevel: 8)
        .Select(level => level.Pivots.Select(p => new { p.Kind, p.Time, p.Price })),
    Prices = new
    {
        T = prices.Select(p => p.DateTime),
        O = prices.Select(p => p.Open),
        H = prices.Select(p => p.High),
        L = prices.Select(p => p.Low),
        C = prices.Select(p => p.Close),
    },
};

var fullPath = Path.Combine(repoRoot, "CryptoAnalysis.Test", "TestData", "PricesExtensionsData", "COINBASE_BTCUSD, 60", "COINBASE_BTCUSD, 60.csv");
var full = File.Exists(fullPath)
    ? new CsvReaderHelper().ReadData<Price, PriceClassMap>(fullPath).ToList()
    : [];
var sawtoothExamples = new List<object>();
if (full.Count > 0)
{
    var high = full.First(p => p.Close == full.Max(x => x.Close));
    sawtoothExamples.Add(SawtoothExample("Sawtooth/full-history", "Full history",
        $"All {full.Count:N0} hourly BTC candles in the dataset ({full[0].DateTime:d MMM yyyy} to {full[^1].DateTime:d MMM yyyy}), starting at {full[0].Close:N0}. " +
        $"Level 0 is the widest view: the first price up to the highest close ({high.Close:N0} on {high.DateTime:d MMM yyyy}), then the incomplete leg to the latest close ({full[^1].Close:N0}). " +
        "Step through the levels to see the upleg and the presumed death-leg split into their own highs and lows.",
        full));
}

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

var data = new { Generated = DateTime.UtcNow, Terms = terms, Examples = examples, Structure = structure };
var json = JsonSerializer.Serialize(data, new JsonSerializerOptions(TermAnnotations.JsonOptions) { WriteIndented = false });
File.WriteAllText(Path.Combine(outDir, "data.js"), $"window.SITE_DATA = {json};\n");

var mismatched = examples.Count(x => !x.Matches);
Console.WriteLine($"{terms.Count} terms, {examples.Count} examples ({mismatched} mismatched) -> {Path.Combine(outDir, "index.html")}");
return 0;

static List<List<SwingOutline>> SwingsByLevel(List<Price> prices)
{
    var levels = Sawtooth.Levels(prices, EnumPriceBasis.Close, maxLevel: 8);
    return Enumerable.Range(0, levels.Count).Select(l => Sawtooth.Swings(prices, levels, EnumPriceBasis.Close, l)).ToList();
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
