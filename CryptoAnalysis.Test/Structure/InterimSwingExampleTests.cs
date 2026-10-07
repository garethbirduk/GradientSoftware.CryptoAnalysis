using System.Text.Json;

namespace Gradient.CryptoAnalysis.Test.Structure;

/// <summary>
/// Runs every example under TestData/Structure/InterimSwings: the detected swing tree must match the sidecar.
/// To add an example, drop in a CSV and a sidecar with an empty "expected" list, run the tests, review the
/// written actual sidecar against the chart, then copy it over the original.
/// </summary>
[TestClass]
public class InterimSwingExampleTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "TestData", "Structure", "InterimSwings");
    private static readonly string ActualRoot = Path.Combine(AppContext.BaseDirectory, "InterimSwingExamplesActual");

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
        var example = file.LoadSidecar<InterimSwingExample>();
        var actual = InterimSwings.Detect(file.LoadPrices(), example);

        if (actual.SequenceEqual(example.Expected))
            return;

        var actualPath = Path.Combine(ActualRoot, id + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(actualPath)!);
        var actualExample = new InterimSwingExample
        {
            Title = example.Title,
            Description = example.Description,
            Reviewed = false,
            CloseType = example.CloseType,
            Level = example.Level,
            Depth = example.Depth,
            Expected = actual,
        };
        File.WriteAllText(actualPath, JsonSerializer.Serialize(actualExample, TermAnnotations.JsonOptions));

        var missing = example.Expected.Except(actual).Select(x => $"  missing: {x}");
        var extra = actual.Except(example.Expected).Select(x => $"  extra:   {x}");
        Assert.Fail($"Detected swing tree differs from {id}.json\n{string.Join("\n", missing.Concat(extra))}\nActual sidecar written to {actualPath}");
    }

    [TestMethod]
    public void Examples_AreReviewed()
    {
        var unreviewed = TermExampleLibrary.Discover(Root).Where(x => !x.LoadSidecar<InterimSwingExample>().Reviewed).Select(x => x.Id).ToList();

        if (unreviewed.Count > 0)
            Assert.Inconclusive($"Unreviewed examples: {string.Join(", ", unreviewed)}");
    }
}
