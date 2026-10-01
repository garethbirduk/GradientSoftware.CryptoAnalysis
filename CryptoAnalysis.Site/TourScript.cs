using System.Globalization;
using System.Text.Json.Nodes;

namespace Gradient.CryptoAnalysis.Site;

/// <summary>
/// One text of a tour section. At is the candle it appears at (null: as the section begins), On what it is pinned to.
/// </summary>
public sealed record TourCue(int Index, string Text, int? At, string? On, string? Place, string Keep, double Hold);

/// <summary>
/// A tour section with everything worked out: the layers on, the sawtooth levels looked at, the candles it runs from and to.
/// </summary>
public sealed record TourSection(
    int Index,
    string? Chapter,
    IReadOnlyList<string> Layers,
    IReadOnlyList<int> Levels,
    int From,
    int Until,
    IReadOnlyList<int> View,
    double Speed,
    IReadOnlyList<TourCue> Cues);

/// <summary>
/// A tour as compiled from tour.json against a dataset: its sections, the candle its start time lands on, and whatever the
/// file gets wrong.
/// </summary>
public sealed record TourScript(string? Title, string? Dataset, int Anchor, IReadOnlyList<TourSection> Sections, IReadOnlyList<string> Errors);

/// <summary>
/// Reads the page's tour.json the way the page does (see the Tour section of index.html), so a test can check the file and the
/// structure it describes, and the server can resolve the events a tour refers to.
/// A candle in the file is a number counted from the tour's start, or an event to look for from a candle on:
/// { "event": "BullishBreakOfStructure", "level": 1, "nth": 1 }, the nth time that becomes known at that level.
/// </summary>
public static class Tours
{
    /// <summary>
    /// The layers the page draws that are not terms.
    /// </summary>
    public static readonly IReadOnlySet<string> PageLayers = new HashSet<string>(
    [
        "close", "price", "sawtooth", "bosLevelUp", "bosLevelDown", "msbLevelUp", "msbLevelDown",
        "replay", "candidates", "future", "eventlog", "live",
        "ghostSwings", "ghostCandidates", "ghostPoints", "ghostTrends", "ghostMsbs",
        .. Enumerable.Range(0, 9).Select(x => $"level{x}"),
    ]);

    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string> { ["candles"] = "price", ["closes"] = "close" };
    private static readonly string[] Pins = ["open", "high", "low", "close"];
    private static readonly string[] Places = ["above", "below", "left", "right"];
    private static readonly string[] Keeps = ["section", "chapter", "always"];
    private static readonly int[] DefaultView = [0, 168];
    private const int EventSearchLimit = 3000;

    /// <summary>
    /// Compiles the tour against a dataset: the candle its start time lands on, then every section worked out from the start
    /// state and the sections before it. Problems are listed, not thrown.
    /// </summary>
    public static TourScript Compile(JsonNode def, IReadOnlyList<Price> dataset)
    {
        var errors = new List<string>();
        var known = new HashSet<string>(PageLayers.Concat(Terms.All.Select(x => x.Type.ToString())));
        var start = def["start"]?.AsObject();
        var anchor = AnchorOf(start?["time"]?.GetValue<string>(), dataset);
        var prices = dataset.Skip(anchor).ToList();
        var candles = prices.Count;

        List<string> Ids(JsonNode? list, string where) => (list?.AsArray() ?? [])
            .Select(x => x?.GetValue<string>() ?? "")
            .Select(x => Aliases.TryGetValue(x, out var id) ? id : x)
            .Where(x => known.Contains(x) || !Add(errors, $"{where}: unknown layer \"{x}\""))
            .ToList();

        int[] Range(JsonNode? node, string where, int[] fallback)
        {
            var values = node?.AsArray().Select(x => x?.GetValue<int>()).ToArray();
            if (values is [int first, int last] && first >= 0 && last > first)
                return [first, last];
            errors.Add($"{where}: the view is the first and last candle shown, the last after the first");
            return fallback;
        }

        double Rate(JsonNode? node, string where, double fallback)
        {
            var value = node?.GetValue<double>() ?? fallback;
            if (value > 0)
                return value;
            errors.Add($"{where}: speed must be more than 0");
            return fallback;
        }

        var layers = Ids(start?["layers"], "start");
        var view = start?["view"] is JsonNode startView ? Range(startView, "start", DefaultView) : DefaultView;
        var speed = Rate(start?["speed"], "start", 4);
        var at = 0;
        var sections = new List<TourSection>();

        foreach (var (node, i) in (def["sections"]?.AsArray() ?? []).Select((x, i) => (x?.AsObject(), i)))
        {
            var where = $"section {i + 1}";
            if (node == null)
            {
                errors.Add($"{where}: not a section");
                continue;
            }

            if (node["window"] != null)
                errors.Add($"{where}: \"window\" is now \"view\", the first and last candle shown");
            if (node["layers"] != null)
                layers = Ids(node["layers"], where);
            var removed = Ids(node["remove"], where);
            layers = layers.Concat(Ids(node["add"], where)).Distinct().Where(x => !removed.Contains(x)).ToList();
            if (node["view"] != null)
                view = Range(node["view"], where, view);
            if (node["speed"] != null)
                speed = Rate(node["speed"], where, speed);

            var from = Candle(node["from"], prices, at, where, "from", errors) ?? at;
            if (from < 0 || from >= candles)
            {
                errors.Add($"{where}: from #{from} is not a candle of the tour, which has #0 to #{candles - 1}");
                from = at;
            }

            var until = Candle(node["until"], prices, from, where, "until", errors) ?? from;
            if (until < from)
                errors.Add($"{where}: until #{until} is before the section's start, #{from}");
            if (until >= candles)
                errors.Add($"{where}: until #{until} is past the last candle, #{candles - 1}");
            at = Math.Max(from, Math.Min(until, candles - 1));

            var cues = new List<TourCue>();
            foreach (var (cue, j) in (node["cues"]?.AsArray() ?? []).Select((x, j) => (x?.AsObject(), j)))
            {
                var what = $"{where}, text {j + 1}";
                var text = cue?["text"]?.GetValue<string>() ?? "";
                if (text.Length == 0)
                    errors.Add($"{what}: no text");
                var cueAt = Candle(cue?["at"], prices, from, what, "at", errors);
                if (cueAt is int c && (c < 0 || c > at))
                    errors.Add($"{what}: at #{c} is not reached, as the section ends at #{at}");
                var on = cue?["on"] is JsonValue onValue ? onValue.ToString() : null;
                if (on != null && cueAt == null)
                    errors.Add($"{what}: a pin needs a candle");
                if (on != null && !Pins.Contains(on) && !double.TryParse(on, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    errors.Add($"{what}: unknown \"on\" \"{on}\"");
                var place = cue?["place"]?.GetValue<string>();
                if (place != null && !Places.Contains(place))
                    errors.Add($"{what}: unknown place \"{place}\"");
                var keep = cue?["keep"]?.GetValue<string>() ?? "section";
                if (!Keeps.Contains(keep))
                    errors.Add($"{what}: unknown keep \"{keep}\"");
                var hold = cue?["hold"]?.GetValue<double>();
                cues.Add(new TourCue(j, text, cueAt, on, place, keep, hold >= 0 ? hold.Value : Math.Max(2.5, text.Length / 14.0)));
            }

            var levels = layers.Where(x => x.StartsWith("level")).Select(x => int.Parse(x["level".Length..])).OrderBy(x => x).ToList();
            sections.Add(new TourSection(i, node["chapter"]?.GetValue<string>(), layers, levels.Count > 0 ? levels : [1], from, at, view, speed, cues));
        }

        return new TourScript(def["title"]?.GetValue<string>(), def["dataset"]?.GetValue<string>(), anchor, sections, errors);
    }

    /// <summary>
    /// The candle of the dataset a tour's start time lands on: the first at or after it, or the first candle with no time.
    /// </summary>
    public static int AnchorOf(string? time, IReadOnlyList<Price> dataset)
    {
        if (time == null || !DateTime.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var start))
            return 0;
        var index = dataset.ToList().FindIndex(x => DateTime.SpecifyKind(x.DateTime, DateTimeKind.Utc) >= start);
        return index < 0 ? dataset.Count - 1 : index;
    }

    /// <summary>
    /// The candle at which an event is known for the nth time at a level, looking from the candle after from, with only the
    /// prices up to each candle: the first close that breaks structure, or the candle a higher high is first made on. Null
    /// when it does not happen within the search limit.
    /// </summary>
    public static int? FindEvent(List<Price> prices, int from, EnumAnnotationType type, int level, int nth = 1)
    {
        var isPoint = Terms.Get(type).Category == TermCategories.StructurePoints;
        var seen = new HashSet<DateTime>();
        var count = 0;
        for (var t = from + 1; t < prices.Count && t <= from + EventSearchLimit; t++)
        {
            var prefix = prices.GetRange(0, t + 1);
            var last = prefix[^1].DateTime;
            bool known;
            if (isPoint)
            {
                var levels = Sawtooth.Levels(prefix, EnumPriceBasis.Close, level);
                known = level < levels.Count && Sawtooth.Points(prefix, levels[level], EnumPriceBasis.Close)
                    .Any(x => x.Type == type && x.Time == last && seen.Add(x.LegStart));
            }
            else
            {
                known = MarketStructure.EventsOnLastCandle(prefix, EnumPriceBasis.Close, level).Any(x => x.Type == type);
            }

            if (known && ++count == nth)
                return t;
        }

        return null;
    }

    /// <summary>
    /// What the structure has at each candle a tour points at, as known when the section shows it: the labels of its points,
    /// the swings starting, turning or breaking there, and the market structure breaks, at every level the section looks at.
    /// One line per text with a candle and per section that runs, so a change in the structure code shows up as a change in
    /// these lines.
    /// </summary>
    public static List<string> Facts(TourScript tour, IReadOnlyList<Price> dataset)
    {
        var prices = dataset.Skip(tour.Anchor).ToList();
        var lines = new List<string>();
        foreach (var s in tour.Sections)
        {
            var name = s.Chapter != null ? $"section {s.Index + 1} ({s.Chapter})" : $"section {s.Index + 1}";
            if (s.Until > s.From)
                lines.Add($"#{s.Until} · {name} runs to it: {At(prices, s.Until, s.Until, s.Levels)}");
            foreach (var c in s.Cues.Where(x => x.At != null))
                lines.Add($"#{c.At} · {name} text {c.Index + 1} \"{c.Text}\"{(c.On != null ? $" on {c.On}" : "")} · seen at #{s.Until}: {At(prices, c.At!.Value, s.Until, s.Levels)}");
        }

        return lines;
    }

    private static string At(List<Price> prices, int candle, int cursor, IReadOnlyList<int> levels)
    {
        if (candle < 0 || candle >= prices.Count || cursor >= prices.Count)
            return "not a candle of the tour";
        var prefix = prices.GetRange(0, cursor + 1);
        var time = prices[candle].DateTime;
        var sawtooth = Sawtooth.Levels(prefix, EnumPriceBasis.Close, 8);
        var facts = new List<string>();
        foreach (var level in levels.Where(x => x < sawtooth.Count))
        {
            facts.AddRange(Sawtooth.Points(prefix, sawtooth[level], EnumPriceBasis.Close)
                .Where(x => x.Time == time && x.Type != null)
                .Select(x => $"L{level} {x.Label}{(x.Provisional ? "*" : "")}"));
            var swings = Sawtooth.Swings(prefix, sawtooth, EnumPriceBasis.Close, level);
            foreach (var swing in swings)
            {
                var kind = swing.Direction == EnumSwingDirection.Up ? "upswing" : "downswing";
                if (swing.Start.Time == time)
                    facts.Add($"L{level} {kind} start");
                if (swing.Extreme.Time == time)
                    facts.Add($"L{level} {kind} {(swing.Direction == EnumSwingDirection.Up ? "low" : "high")}");
                if (swing.BreakOfStructure?.Time == time)
                    facts.Add($"L{level} {kind} BoS");
            }

            facts.AddRange(Sawtooth.MarketStructureBreaks(prefix, swings, EnumPriceBasis.Close)
                .Where(x => x.Break.Time == time)
                .Select(x => $"L{level} {Terms.Get(x.Type).Label}"));
        }

        return facts.Count > 0 ? string.Join(" · ", facts.Distinct()) : "nothing";
    }

    // A candle given as a number, or as an event to look for from a candle on.
    private static int? Candle(JsonNode? node, List<Price> prices, int from, string where, string field, List<string> errors)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonValue value when value.TryGetValue<int>(out var number):
                return number;
            case JsonObject spec:
            {
                var name = spec["event"]?.GetValue<string>() ?? "";
                if (!Enum.TryParse<EnumAnnotationType>(name, out var type) || type == EnumAnnotationType.None)
                {
                    errors.Add($"{where}: {field} names an unknown event \"{name}\"");
                    return null;
                }

                var level = spec["level"]?.GetValue<int>() ?? 1;
                var nth = spec["nth"]?.GetValue<int>() ?? 1;
                var found = FindEvent(prices, from, type, level, nth);
                if (found == null)
                    errors.Add($"{where}: {field} waits for {Ordinal(nth)} {name} at level {level} after #{from}, which does not happen");
                return found;
            }
            default:
                errors.Add($"{where}: {field} must be a candle number or an event");
                return null;
        }
    }

    private static string Ordinal(int n) => n switch { 1 => "the first", 2 => "the second", 3 => "the third", _ => $"the {n}th" };

    private static bool Add(List<string> errors, string error)
    {
        errors.Add(error);
        return true;
    }
}
