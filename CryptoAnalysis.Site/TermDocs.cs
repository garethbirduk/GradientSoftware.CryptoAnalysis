using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Gradient.CryptoAnalysis.Site;

/// <summary>
/// Reads term definitions from the core library's XML documentation file.
/// </summary>
public sealed class TermDocs
{
    private readonly Dictionary<string, XElement> _members;

    private TermDocs(Dictionary<string, XElement> members)
    {
        _members = members;
    }

    /// <summary>
    /// Loads the documentation file that sits next to the core assembly.
    /// </summary>
    public static TermDocs Load()
    {
        var path = Path.ChangeExtension(typeof(Terms).Assembly.Location, ".xml");
        if (!File.Exists(path))
            throw new FileNotFoundException("Core XML documentation not found; is GenerateDocumentationFile on?", path);

        var members = XDocument.Load(path).Descendants("member")
            .Where(x => x.Attribute("name") != null)
            .ToDictionary(x => x.Attribute("name")!.Value);

        return new TermDocs(members);
    }

    /// <summary>
    /// Gets the summary and remarks for an annotation type, with cref links reduced to `Type.Member` code spans.
    /// </summary>
    public (string Summary, string Remarks) For(EnumAnnotationType type)
    {
        if (!_members.TryGetValue($"F:{typeof(EnumAnnotationType).FullName}.{type}", out var member))
            return ("", "");

        return (Text(member.Element("summary")), Text(member.Element("remarks")));
    }

    private static string Text(XElement? element)
    {
        if (element == null)
            return "";

        foreach (var see in element.Descendants("see").ToList())
        {
            var cref = see.Attribute("cref")?.Value ?? "";
            var name = Regex.Replace(cref[(cref.IndexOf(':') + 1)..], @"\(.*\)$", "");
            var parts = name.Split('.');
            see.ReplaceWith($"`{string.Join('.', parts.TakeLast(cref.StartsWith("T:") ? 1 : 2))}`");
        }

        return Regex.Replace(element.Value, @"\s+", " ").Trim();
    }
}
