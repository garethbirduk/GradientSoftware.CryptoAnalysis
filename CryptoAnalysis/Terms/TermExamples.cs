using CryptoAnalysis.Csv.ClassMaps;
using Gradient.CryptoAnalysis.Csv;
using System.Text.Json;

namespace Gradient.CryptoAnalysis;

/// <summary>
/// The sidecar describing one real-data example of a term: what it shows and the annotations a human expects.
/// </summary>
public sealed class TermExample
{
    public EnumAnnotationType Term { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>
    /// Set to true once a human has checked the expected annotations against the chart.
    /// Until then the expectations are only what the detector produced when the example was captured.
    /// </summary>
    public bool Reviewed { get; set; }

    public EnumCloseType CloseType { get; set; } = EnumCloseType.Close;

    /// <summary>
    /// The sawtooth level the structure points (HH, HL, LH, LL) are taken from.
    /// </summary>
    public int Level { get; set; } = 1;
    public List<TermAnnotation> Expected { get; set; } = [];
}

/// <summary>
/// An example on disk: <c>&lt;root&gt;/&lt;Term&gt;/&lt;name&gt;.csv</c> holds the prices and <c>&lt;name&gt;.json</c> the sidecar.
/// </summary>
public sealed record TermExampleFile(string Id, string CsvPath, string JsonPath)
{
    /// <summary>
    /// Reads the sidecar.
    /// </summary>
    public TermExample LoadExample()
    {
        return JsonSerializer.Deserialize<TermExample>(File.ReadAllText(JsonPath), TermAnnotations.JsonOptions)
            ?? throw new InvalidDataException($"Empty example sidecar: {JsonPath}");
    }

    /// <summary>
    /// Reads the price snippet.
    /// </summary>
    public List<Price> LoadPrices()
    {
        return new CsvReaderHelper().ReadData<Price, PriceClassMap>(CsvPath).ToList();
    }

    /// <summary>
    /// Annotates the snippet and keeps only the example's own term, which is what the sidecar's expectations cover.
    /// </summary>
    public List<TermAnnotation> Detect(TermExample example)
    {
        return TermAnnotations.Annotate(LoadPrices(), example.CloseType, example.Level).Where(x => x.Type == example.Term).ToList();
    }
}

/// <summary>
/// Finds term examples under a root folder.
/// </summary>
public static class TermExampleLibrary
{
    /// <summary>
    /// Lists every example whose CSV has a matching JSON sidecar, ordered by term folder then name.
    /// </summary>
    public static List<TermExampleFile> Discover(string root)
    {
        if (!Directory.Exists(root))
            return [];

        return Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
            .Select(json => (Json: json, Csv: Path.ChangeExtension(json, ".csv")))
            .Where(x => File.Exists(x.Csv))
            .Select(x => new TermExampleFile(
                Path.GetRelativePath(root, Path.ChangeExtension(x.Json, null)).Replace('\\', '/'),
                x.Csv,
                x.Json))
            .OrderBy(x => x.Id, StringComparer.Ordinal)
            .ToList();
    }
}
