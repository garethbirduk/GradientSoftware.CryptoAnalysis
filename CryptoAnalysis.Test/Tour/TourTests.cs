using System.Text.Json;
using System.Text.Json.Nodes;
using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Csv;
using Gradient.CryptoAnalysis.Site;
using Gradient.CryptoAnalysis.Strategies;

namespace Gradient.CryptoAnalysis.Test.Tour;

/// <summary>
/// Checks the site's tour (CryptoAnalysis.Site/wwwroot/tour.json) against the dataset it plays on: the file must compile
/// without problems, and what the structure has at every candle the tour points at must still be what it was when the tour
/// was written, so a change to the structure code cannot quietly move the things the texts describe.
/// The expected facts live in TestData/Tour/tour.expected.txt. When they differ, the actual facts are written beside them;
/// review the change against the tour, then copy the actual file over the expected one. Delete the expected file to capture
/// it afresh.
/// </summary>
[TestClass]
public class TourTests
{
    private static readonly string RepoRoot = FindRepoRoot(AppContext.BaseDirectory);
    private static readonly string TourPath = Path.Combine(RepoRoot, "CryptoAnalysis.Site", "wwwroot", "tour.json");
    private static readonly string ExpectedPath = Path.Combine(RepoRoot, "CryptoAnalysis.Test", "TestData", "Tour", "tour.expected.txt");
    private static readonly string ActualPath = Path.Combine(RepoRoot, "CryptoAnalysis.Test", "TestData", "Tour", "tour.actual.txt");

    // The datasets the server serves, loaded once, so a tour with scenes on several of them compiles as the page plays it.
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<Price>>> Datasets = new(() =>
        Tours.Datasets.ToDictionary(x => x.Id, x => (IReadOnlyList<Price>)Tours.Load(x, RepoRoot)));

    private static TourScript Compile()
    {
        var def = JsonNode.Parse(File.ReadAllText(TourPath)) ?? throw new InvalidOperationException("tour.json is empty");
        return Tours.Compile(def, Datasets.Value, teachOnce: true);
    }

    [TestMethod]
    public void Tour_CompilesWithoutProblems()
    {
        var tour = Compile();

        Assert.IsTrue(tour.Sections.Count > 0, "The tour has no sections.");
        Assert.AreEqual(0, tour.Errors.Count, $"tour.json has problems:\n{string.Join("\n", tour.Errors.Select(x => "  " + x))}");
    }

    [TestMethod]
    public void TourStrategies_AreLayersAndTheirProblemsListed()
    {
        var def = JsonNode.Parse("""
            { "strategies": [
                { "id": "ok", "entry": { "type": "SuccessiveCandles", "colour": "Green", "length": 3 }, "takeProfit": { "type": "Percent", "percent": 1 }, "stopLoss": { "type": "Percent", "percent": 1 } },
                { "id": "bad", "takeProfit": { "type": "Percent", "percent": 0 } },
                { "name": "no id" },
                { "id": "ok" } ] }
            """)!;
        var errors = new List<string>();

        CollectionAssert.AreEqual(new[] { "ok", "bad", "ok" }, Tours.TourStrategies(def, errors));
        Assert.IsTrue(errors.Any(x => x.StartsWith("strategy 2 (bad): Take Profit")), string.Join("\n", errors));
        Assert.IsTrue(errors.Any(x => x.StartsWith("strategy 3: a strategy needs an id")), string.Join("\n", errors));
        Assert.IsTrue(errors.Any(x => x.StartsWith("strategy 4: \"ok\" is the id of a strategy before it")), string.Join("\n", errors));
    }

    [TestMethod]
    public void TourResults_ProblemsListed()
    {
        var strategies = new Dictionary<string, JsonNode>
        {
            ["plain"] = JsonNode.Parse("""{ "id": "plain" }""")!,
            ["varied"] = JsonNode.Parse("""{ "id": "varied", "vary": { "takeProfit.percent": { "to": 3, "step": 0.5 } } }""")!,
        };
        List<string> Problems(string json) => Tours.ResultsProblems(JsonNode.Parse(json)!, strategies);

        Assert.AreEqual(0, Problems("""{ "show": "sweep", "strategy": "varied", "period": "btc-coinbase-1h:2023" }""").Count);
        Assert.AreEqual(0, Problems("""{ "show": "baseline", "strategy": "plain", "period": "btc-coinbase-1h:2023" }""").Count);
        Assert.IsTrue(Problems("""{ "show": "graph", "strategy": "plain", "period": "btc-coinbase-1h:2023" }""").Single().StartsWith("results show one of"));
        Assert.IsTrue(Problems("""{ "show": "run", "strategy": "none", "period": "btc-coinbase-1h:2023" }""").Single().EndsWith("there is no \"none\""));
        Assert.IsTrue(Problems("""{ "show": "sweep", "strategy": "plain", "period": "btc-coinbase-1h:2023" }""").Single().Contains("has no \"vary\""));
        Assert.IsTrue(Problems("""{ "show": "run", "strategy": "plain", "period": "nowhere:2023" }""").Single().StartsWith("results need \"period\""));
    }

    [TestMethod]
    public void Tour_StrategyChapterTradesWhereItsTextsSay()
    {
        var def = JsonNode.Parse(File.ReadAllText(TourPath))!;
        var strategy = def["strategies"]!.AsArray().Single(x => x?["id"]?.GetValue<string>() == "three-green")
            .Deserialize<Strategy>(StrategyBook.JsonOptions)!;
        var prices = Datasets.Value["btc-1h"];
        var anchor = prices.ToList().FindIndex(x => x.DateTime == new DateTime(2023, 2, 14, 7, 0, 0, DateTimeKind.Utc));

        var trades = StrategyBacktest.Run(prices.ToList(), strategy).Trades
            .Where(x => x.EntryIndex >= anchor && x.EntryIndex <= anchor + 56)
            .Select(x => $"{x.EntryIndex - anchor}-{x.ExitIndex - anchor} {x.Outcome}");

        Assert.AreEqual("12-28 TakeProfit, 28-29 TakeProfit, 36-37 TakeProfit, 42-54 StopLoss", string.Join(", ", trades));
    }

    [TestMethod]
    public void Tour_StructureMatchesExpected()
    {
        var tour = Compile();
        var actual = Tours.Facts(tour, Datasets.Value);

        if (!File.Exists(ExpectedPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ExpectedPath)!);
            File.WriteAllLines(ExpectedPath, actual);
            Assert.Inconclusive($"Captured {actual.Count} facts to {ExpectedPath}; review them against the tour.");
        }

        var expected = File.ReadAllLines(ExpectedPath).Where(x => x.Length > 0).ToList();
        if (actual.SequenceEqual(expected))
        {
            if (File.Exists(ActualPath))
                File.Delete(ActualPath);
            return;
        }

        File.WriteAllLines(ActualPath, actual);
        var missing = expected.Except(actual).Select(x => $"  was:  {x}");
        var extra = actual.Except(expected).Select(x => $"  now:  {x}");
        Assert.Fail($"The structure at the candles the tour points at has changed.\n{string.Join("\n", missing.Concat(extra))}\nActual facts written to {ActualPath}");
    }

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
