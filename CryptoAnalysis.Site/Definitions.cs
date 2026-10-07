using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Gradient.CryptoAnalysis.Site;

/// <summary>
/// The definitions of the terms, each rule a sentence or two, kept once in wwwroot/definitions.json and used by the tour
/// (a cue with "define"), by the explanations of an analysis and by the page. A definition can have slots for the
/// direction it is told in: {trend}, {a trend}, {swing}, {a swing}, {other swing}, {a other swing}, {turn}, {beyond},
/// {weak turn}, {turn labels}, {extreme}, {trends}, {start}, {a first leg} and {a second leg}. Each sentence then starts with a capital, whatever fills it. The page fills them the same way (see
/// defineText in index.html), so a definition's clip is found by either.
/// </summary>
public static class Definitions
{
    private static readonly object Gate = new();
    private static string file = Path.Combine(AppContext.BaseDirectory, "wwwroot", "definitions.json");
    private static (DateTime Written, IReadOnlyDictionary<string, string> Read)? read;

    /// <summary>
    /// Reads the definitions from a file of their own from now on: the server's source copy, which the tour's editor saves.
    /// </summary>
    public static void UseFile(string path)
    {
        lock (Gate)
        {
            file = path;
            read = null;
        }
    }

    // The definitions as the file has them now: read again whenever it has been written since.
    private static IReadOnlyDictionary<string, string> All
    {
        get
        {
            lock (Gate)
            {
                var written = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : default;
                if (read?.Written != written)
                    read = (written, Load(file));
                return read.Value.Read;
            }
        }
    }

    /// <summary>
    /// The definitions in a file, by key.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Load(string path)
    {
        if (!File.Exists(path))
            return new Dictionary<string, string>();
        var json = JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? [];
        return json.ToDictionary(x => x.Key, x => x.Value?.GetValue<string>() ?? "");
    }

    /// <summary>
    /// A term's definitions as its page lists them, told in its direction: those of its family, as Uptrend and Downtrend
    /// are both of Trend. None for a term that has none yet.
    /// </summary>
    public static IReadOnlyList<string> Of(EnumAnnotationType type)
    {
        var (family, direction) = type switch
        {
            EnumAnnotationType.Uptrend => ("Trend", EnumSwingDirection.Up),
            EnumAnnotationType.Downtrend => ("Trend", EnumSwingDirection.Down),
            EnumAnnotationType.Upswing => ("Swing", EnumSwingDirection.Up),
            EnumAnnotationType.Downswing => ("Swing", (EnumSwingDirection?)EnumSwingDirection.Down),
            _ => ("", null),
        };
        return family.Length == 0 ? [] : All.Keys.Where(x => x.StartsWith($"{family}.", StringComparison.Ordinal)).Select(x => Text(x, direction)).ToList();
    }

    /// <summary>
    /// Whether there is a definition by that key.
    /// </summary>
    public static bool Has(string key) => All.ContainsKey(key);

    /// <summary>
    /// Whether a definition has slots, and so needs a direction to be told in.
    /// </summary>
    public static bool NeedsDirection(string key) => All.TryGetValue(key, out var text) && text.Contains('{');

    /// <summary>
    /// A definition told in a direction, its slots filled and each sentence starting with a capital.
    /// </summary>
    public static string Text(string key, EnumSwingDirection? direction = null)
    {
        var text = All[key];
        if (direction is { } d)
        {
            var up = d == EnumSwingDirection.Up;
            var slots = new Dictionary<string, string>
            {
                ["trend"] = up ? "Uptrend" : "Downtrend",
                ["a trend"] = up ? "an Uptrend" : "a Downtrend",
                ["swing"] = up ? "Upswing" : "Downswing",
                ["a swing"] = up ? "an Upswing" : "a Downswing",
                ["other swing"] = up ? "Downswing" : "Upswing",
                ["a other swing"] = up ? "a Downswing" : "an Upswing",
                ["turn"] = up ? "Swing Low" : "Swing High",
                ["beyond"] = up ? "below" : "above",
                ["weak turn"] = up ? "LL" : "HH",
                ["turn labels"] = up ? "an HL or an LL" : "an LH or an HH",
                ["extreme"] = up ? "lowest" : "highest",
                ["trends"] = up ? "upwards" : "downwards",
                ["start"] = up ? "high" : "low",
                ["a first leg"] = up ? "a Downleg" : "an Upleg",
                ["a second leg"] = up ? "an Upleg" : "a Downleg",
            };
            text = Regex.Replace(text, @"\{([a-z ]+)\}", m => slots.TryGetValue(m.Groups[1].Value, out var said) ? said : m.Value);
        }

        return Regex.Replace(text, @"(^|[.!?]\s+)([a-z])", m => m.Groups[1].Value + char.ToUpperInvariant(m.Groups[2].Value[0]));
    }
}
