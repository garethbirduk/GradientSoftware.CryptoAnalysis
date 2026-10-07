namespace Gradient.CryptoAnalysis;

/// <summary>
/// Declares how an <see cref="EnumAnnotationType"/> member is labelled and drawn wherever it appears.
/// The member's XML documentation carries its definition.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class TermAttribute : Attribute
{
    public TermAttribute(string label, string name, string category, EnumPosition position, string color, string symbol)
    {
        Label = label;
        Name = name;
        Category = category;
        Position = position;
        Color = color;
        Symbol = symbol;
    }

    public string Label { get; }

    public string Name { get; }

    public string Category { get; }

    public EnumPosition Position { get; }

    public string Color { get; }

    public string Symbol { get; }
}
