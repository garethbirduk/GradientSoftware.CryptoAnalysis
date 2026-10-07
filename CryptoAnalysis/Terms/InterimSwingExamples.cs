namespace Gradient.CryptoAnalysis;

/// <summary>
/// One swing in a flattened swing tree. Parent is the start time of the swing one level coarser that it is an interim of,
/// or null for a swing at the example's own level.
/// </summary>
public sealed record InterimSwing(int Level, EnumSwingDirection Direction, DateTime Start, DateTime Break, DateTime? Parent);

/// <summary>
/// The sidecar describing one real-data example of interim swings: the swings at a level and their interims, depth levels finer.
/// </summary>
public sealed class InterimSwingExample
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>
    /// Set to true once a human has checked the expected swings against the chart.
    /// </summary>
    public bool Reviewed { get; set; }

    public EnumCloseType CloseType { get; set; } = EnumCloseType.Close;

    /// <summary>
    /// The sawtooth level of the outermost swings.
    /// </summary>
    public int Level { get; set; } = 1;

    /// <summary>
    /// How many levels of interim swings below the outermost swings are expected.
    /// </summary>
    public int Depth { get; set; } = 1;
    public List<InterimSwing> Expected { get; set; } = [];
}

/// <summary>
/// Flattens swing trees for interim swing examples.
/// </summary>
public static class InterimSwings
{
    /// <summary>
    /// Detects the example's swing tree and flattens it, each swing followed by its interims.
    /// </summary>
    public static List<InterimSwing> Detect(List<Price> prices, InterimSwingExample example)
    {
        return Flatten(TermAnnotations.SwingTree(prices, example.CloseType, example.Level, example.Depth));
    }

    /// <summary>
    /// Flattens a swing tree depth first, each swing followed by its interims.
    /// </summary>
    public static List<InterimSwing> Flatten(IEnumerable<SwingNode> nodes, DateTime? parent = null)
    {
        return nodes
            .SelectMany(node => Flatten(node.Interims, node.Swing.Start.Time)
                .Prepend(new InterimSwing(node.Swing.Level, node.Swing.Direction, node.Swing.Start.Time, node.Swing.End.Time, parent)))
            .ToList();
    }
}
