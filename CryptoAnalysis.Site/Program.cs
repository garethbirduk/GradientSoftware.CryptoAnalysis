using Gradient.CryptoAnalysis;
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
    var annotations = TermAnnotations.Annotate(prices, example.CloseType);
    var detected = annotations.Where(x => x.Type == example.Term).ToList();

    return new
    {
        file.Id,
        example.Term,
        example.Title,
        example.Description,
        example.Reviewed,
        example.CloseType,
        Matches = detected.SequenceEqual(example.Expected),
        Expected = example.Expected,
        Annotations = annotations,
        Swings = TermAnnotations.Swings(prices, example.CloseType),
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

var data = new { Generated = DateTime.UtcNow, Terms = terms, Examples = examples };
var json = JsonSerializer.Serialize(data, new JsonSerializerOptions(TermAnnotations.JsonOptions) { WriteIndented = false });
File.WriteAllText(Path.Combine(outDir, "data.js"), $"window.SITE_DATA = {json};\n");

var mismatched = examples.Count(x => !x.Matches);
Console.WriteLine($"{terms.Count} terms, {examples.Count} examples ({mismatched} mismatched) -> {Path.Combine(outDir, "index.html")}");
return 0;

static string FindRepoRoot(string start)
{
    for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "CryptoAnalysis.sln")))
            return dir.FullName;
    }

    return Directory.GetCurrentDirectory();
}
