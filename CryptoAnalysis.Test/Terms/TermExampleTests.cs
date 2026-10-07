using System.Text.Json;

namespace Gradient.CryptoAnalysis.Test.Terms;

/// <summary>
/// Runs every example under TestData/Terms: the detector's output for the example's term must match the sidecar.
/// To add an example, drop in a CSV and a sidecar with an empty "expected" list, run the tests, review the
/// written actual sidecar against the chart, then copy it over the original.
/// </summary>
[TestClass]
public class TermExampleTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "TestData", "Terms");
    private static readonly string ActualRoot = Path.Combine(AppContext.BaseDirectory, "TermExamplesActual");

    public static IEnumerable<object[]> Examples => TermExampleLibrary.Discover(Root).Select(x => new object[] { x.Id });

    [TestMethod]
    public void Examples_AreDiscovered()
    {
        Assert.IsTrue(TermExampleLibrary.Discover(Root).Count > 0, $"No examples found under {Root}");
    }

    [DataTestMethod]
    [DynamicData(nameof(Examples))]
    public void Example_MatchesExpected(string id)
    {
        var file = TermExampleLibrary.Discover(Root).Single(x => x.Id == id);
        var example = file.LoadExample();
        var actual = file.Detect(example);

        Assert.AreEqual(Path.GetFileName(Path.GetDirectoryName(file.JsonPath)), example.Term.ToString(), "Sidecar term must match its folder.");

        if (actual.SequenceEqual(example.Expected))
            return;

        var actualPath = Path.Combine(ActualRoot, id + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(actualPath)!);
        var actualExample = new TermExample
        {
            Term = example.Term,
            Title = example.Title,
            Description = example.Description,
            Reviewed = false,
            CloseType = example.CloseType,
            Level = example.Level,
            Expected = actual,
        };
        File.WriteAllText(actualPath, JsonSerializer.Serialize(actualExample, TermAnnotations.JsonOptions));

        var missing = example.Expected.Except(actual).Select(x => $"  missing: {x}");
        var extra = actual.Except(example.Expected).Select(x => $"  extra:   {x}");
        Assert.Fail($"Detector output differs from {id}.json\n{string.Join("\n", missing.Concat(extra))}\nActual sidecar written to {actualPath}");
    }

    [TestMethod]
    public void Examples_AreReviewed()
    {
        var unreviewed = TermExampleLibrary.Discover(Root).Where(x => !x.LoadExample().Reviewed).Select(x => x.Id).ToList();

        if (unreviewed.Count > 0)
            Assert.Inconclusive($"Unreviewed examples: {string.Join(", ", unreviewed)}");
    }
}
