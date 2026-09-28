using System.Reflection;

namespace Gradient.CryptoAnalysis;

/// <summary>
/// A single entry in the term registry, built from the <see cref="TermAttribute"/> on an <see cref="EnumAnnotationType"/> member.
/// </summary>
public sealed record Term(
    EnumAnnotationType Type,
    string Label,
    string Name,
    string Category,
    EnumPosition Position,
    string Color,
    string Symbol);

/// <summary>
/// Category names used to group terms in the term library.
/// </summary>
public static class TermCategories
{
    public const string StructurePoints = "Structure points";
    public const string StructureBreaks = "Structure breaks";
    public const string Indicators = "Indicators";
    public const string CandlePatterns = "Candle patterns";
}

/// <summary>
/// Registry of every annotated term, the single source for labels, positions and styling.
/// </summary>
public static class Terms
{
    private static readonly IReadOnlyDictionary<EnumAnnotationType, Term> _terms = Build();

    /// <summary>
    /// All registered terms, in enum declaration order.
    /// </summary>
    public static IReadOnlyList<Term> All { get; } = _terms.Values.ToList();

    /// <summary>
    /// Gets the term registered for the given annotation type.
    /// </summary>
    public static Term Get(EnumAnnotationType type)
    {
        if (_terms.TryGetValue(type, out var term))
            return term;

        throw new ArgumentOutOfRangeException(nameof(type), type, "No term is registered for this annotation type.");
    }

    private static Dictionary<EnumAnnotationType, Term> Build()
    {
        return typeof(EnumAnnotationType)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (Type: (EnumAnnotationType)field.GetValue(null)!, Attribute: field.GetCustomAttribute<TermAttribute>()))
            .Where(x => x.Attribute != null)
            .ToDictionary(
                x => x.Type,
                x => new Term(x.Type, x.Attribute!.Label, x.Attribute.Name, x.Attribute.Category, x.Attribute.Position, x.Attribute.Color, x.Attribute.Symbol));
    }
}
