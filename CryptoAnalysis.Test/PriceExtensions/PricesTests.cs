using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Csv;
using Plotly.NET;

namespace Gradient.CryptoAnalysis.Test.PriceExtensions;

[TestClass]
public abstract class PricesTests
{
    protected static readonly string _cryptoDataDirectory = Path.Combine("TestData");

    protected static readonly string _cryptoDataFilePath = Path.Combine(_cryptoDataDirectory, "TestData.csv");

    protected List<Price> _prices = new();

    protected string SaveChart(GenericChart chart, string filename)
    {
        var path = Path.Combine(DirectoryPath(), filename);
        ChartGenerator.Save(chart, path);
        return path;
    }

    public abstract string TestDirectory { get; }

    public static void AssertHtmlChartFileContent(string expectedPath, string actualPath)
    {
        var expectedLine = File.ReadLines(expectedPath).FirstOrDefault(x => x.TrimStart().StartsWith("var data = ["));
        var actualLine = File.ReadLines(actualPath).FirstOrDefault(x => x.TrimStart().StartsWith("var data = ["));

        Assert.IsNotNull(expectedLine, "Expected 'var data' line not found.");
        Assert.IsNotNull(actualLine, "Actual 'var data' line not found.");
        Assert.AreEqual(expectedLine.Trim(), actualLine.Trim(), "Mismatch in 'var data' line.");
    }

    public void AssertChart(string name, GenericChart chart)
    {
        var actualHtmlPath = SaveChart(chart, $"actual_{name}.html");
        var expectedHtmlPath = Path.Combine(DirectoryPath(), $"expected_{name}.html");
        AssertHtmlChartFileContent(expectedHtmlPath, actualHtmlPath);
    }

    public string DirectoryPath()
    {
        return Path.Combine("TestData", TestDirectory);
    }

    [TestInitialize]
    public void TestInitialize()
    {
        _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(Path.Combine(DirectoryPath(), "TestData.csv")).ToList() ?? new();
    }
}