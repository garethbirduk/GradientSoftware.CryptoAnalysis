using System.Text.Json.Nodes;
using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Csv;
using Gradient.CryptoAnalysis.Site;

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
        return Tours.Compile(def, Datasets.Value);
    }

    [TestMethod]
    public void Tour_CompilesWithoutProblems()
    {
        var tour = Compile();

        Assert.IsTrue(tour.Sections.Count > 0, "The tour has no sections.");
        Assert.AreEqual(0, tour.Errors.Count, $"tour.json has problems:\n{string.Join("\n", tour.Errors.Select(x => "  " + x))}");
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
