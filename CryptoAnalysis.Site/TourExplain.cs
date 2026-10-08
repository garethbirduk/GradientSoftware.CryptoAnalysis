using System.Globalization;
using System.Text.Json.Nodes;

namespace Gradient.CryptoAnalysis.Site;

/// <summary>
/// What an explanation is told with: the page's settings that decide which things are worth telling of. GreenRuns and
/// RedRuns are how many candles of a colour in a row make Successive Candles, as the page's Candles layers have it.
/// </summary>
public sealed record ExplainOptions(int GreenRuns = 5, int RedRuns = 5)
{
    public static readonly ExplainOptions Default = new();

    /// <summary>
    /// The options a tour.json section gives: "runs" as one number for both colours, or { "green", "red" }.
    /// </summary>
    public static ExplainOptions From(JsonNode? node)
    {
        return node switch
        {
            JsonValue value when value.TryGetValue<int>(out var both) => new ExplainOptions(both, both),
            JsonObject each => new ExplainOptions(each["green"]?.GetValue<int>() ?? Default.GreenRuns, each["red"]?.GetValue<int>() ?? Default.RedRuns),
            _ => Default,
        };
    }

    public JsonNode ToJson() => GreenRuns == RedRuns ? JsonValue.Create(GreenRuns) : new JsonObject { ["green"] = GreenRuns, ["red"] = RedRuns };
}

/// <summary>
/// Writes tour sections from the prices. A section of tour.json that says { "explain": "Candle", "at": 48 } is filled in
/// here: its view, the layers it needs, how far the replay runs so the thing explained is drawn, and its texts, worked out
/// from the candle it names, so the same section explains whichever candle the tour points it at. A Swing or a Trend is
/// the one at the section's sawtooth level ("level", 1 unless given) that the candle is in, read as the chart shows it
/// where the section ends. What the section says for itself wins: a view, until or layers it gives are kept, and texts of
/// its own follow the written ones.
/// </summary>
public static class Explain
{
    /// <summary>
    /// The things a section can explain.
    /// </summary>
    public static readonly IReadOnlyList<string> Terms = ["Candle", "SuccessiveCandles", "Point", "Swing", "Trend"];

    /// <summary>
    /// A thing at a candle that can be explained: its kind (one of <see cref="Terms"/>), the sawtooth level it is at when
    /// that matters, and how the page names it. Id is how an address names it: "Candle", "SuccessiveCandles", "Point:1",
    /// "Swing:2".
    /// </summary>
    public sealed record Thing(string Kind, int? Level, string Name)
    {
        public string Id => Level is int level ? $"{Kind}:{level}" : Kind;

        /// <summary>
        /// A thing from its id, named by its kind alone; null when the kind is not one that can be explained.
        /// </summary>
        public static Thing? Parse(string id)
        {
            var parts = id.Trim().Split(':');
            if (!Terms.Contains(parts[0]))
                return null;
            return new Thing(parts[0], parts.Length > 1 && int.TryParse(parts[1], out var level) ? level : null, parts[0]);
        }

        public JsonObject ToJson() => new() { ["id"] = Id, ["kind"] = Kind, ["level"] = Level, ["name"] = Name };
    }

    private const EnumPriceBasis Basis = EnumPriceBasis.Close;
    private const int CandlesBefore = 4;
    private const int CandlesAfter = 3;
    private const int Margin = 6;

    private static readonly string[] Units = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve",
        "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"];
    private static readonly string[] Tens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];
    private static readonly string[] UnitOrdinals = ["", "first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth",
        "eleventh", "twelfth", "thirteenth", "fourteenth", "fifteenth", "sixteenth", "seventeenth", "eighteenth", "nineteenth"];

    /// <summary>
    /// The names of the blocks a term is told in, in the order they are told, for a section's "blocks" to choose from.
    /// Empty for a term that is not told in blocks.
    /// </summary>
    public static IReadOnlyList<string> BlocksOf(string term) => term switch
    {
        "Swing" => SwingBlocks.Select(x => x.Name).ToList(),
        "Trend" => TrendBlocks.Select(x => x.Name).ToList(),
        _ => [],
    };

    /// <summary>
    /// The section with its written parts filled in, from the prices of its scene and the candle the replay has reached
    /// before it (so the section runs on to the thing it explains when that is further on). The section's "explain", "at"
    /// and "level" are taken out, so what comes back compiles as any other section does. "blocks" tells only the blocks it
    /// names (see <see cref="BlocksOf"/>). A written text that is one of the cited definitions, which the tour has already
    /// told, is left out. Problems are listed, not thrown, and a section with a problem comes back with only what it says
    /// for itself.
    /// </summary>
    public static JsonObject Expand(JsonObject node, List<Price> prices, int reached, string where, List<string> errors, IReadOnlySet<string>? cited = null)
    {
        var result = (JsonObject)node.DeepClone();
        result.Remove("explain");
        result.Remove("at");
        result.Remove("level");
        result.Remove("known");
        result.Remove("runs");
        result.Remove("blocks");
        var term = node["explain"]?.GetValue<string>() ?? "";
        if (!Terms.Contains(term))
        {
            errors.Add($"{where}: explain names \"{term}\", which is not a thing the tour can explain: {string.Join(", ", Terms)}");
            return result;
        }

        List<string>? blocks = null;
        if (node["blocks"] is JsonNode chosen)
        {
            blocks = chosen is JsonArray names ? names.Select(x => x?.GetValue<string>() ?? "").ToList() : [];
            var told = BlocksOf(term);
            if (told.Count == 0)
            {
                errors.Add($"{where}: {term} is not told in blocks, so the section has no \"blocks\"");
                return result;
            }

            if (blocks.Count == 0 || blocks.Any(x => !told.Contains(x)))
            {
                errors.Add($"{where}: blocks are a list of the blocks of a {term} to tell: {string.Join(", ", told)}");
                return result;
            }
        }

        var at = Tours.Candle(node["at"], prices, reached, where, "at", errors);
        if (at == null)
        {
            if (node["at"] == null)
                errors.Add($"{where}: explain needs \"at\", the candle to explain");
            return result;
        }

        if (at < 0 || at >= prices.Count)
        {
            errors.Add($"{where}: at #{at} is not a candle of the tour, which has #0 to #{prices.Count - 1}");
            return result;
        }

        var level = node["level"] is JsonValue given && given.TryGetValue<int>(out var l) ? l : 1;
        var until = node["until"] is JsonValue end && end.TryGetValue<int>(out var u) ? u : (int?)null;
        var known = node["known"] is JsonValue read && read.TryGetValue<int>(out var k) ? k : (int?)null;
        var written = Write(term, prices, at.Value, level, reached, until, known, ExplainOptions.From(node["runs"]), blocks, out var problem);
        if (written == null)
        {
            errors.Add($"{where}: {problem}");
            return result;
        }

        foreach (var (key, value) in written.Where(x => x.Key != "cues"))
            result[key] ??= value?.DeepClone();
        // A definition is taught once, where the tour first tells it; a written section does not teach it again.
        var cues = new JsonArray();
        foreach (var cue in (written["cues"]?.AsArray() ?? []).Where(x => cited == null || Taught(x) is not { } text || !cited.Contains(text)).Concat(node["cues"]?.AsArray() ?? []))
            cues.Add(cue?.DeepClone());
        result["cues"] = cues;
        return result;
    }

    /// <summary>
    /// What a cue teaches: its text when it has one at Education alone, as a written section's teaching does; else null.
    /// </summary>
    public static string? Taught(JsonNode? cue) => cue?["texts"] is JsonObject texts && texts.Count == 1
        ? texts[Details.Name(EnumDetail.Education)]?.GetValue<string>()
        : null;

    /// <summary>
    /// What is at a candle, as the chart had it at the cursor it was seen from (seen, the Replay page's; else the end of
    /// the prices), from the candle outwards: the Candle; the Successive Candles it is in, when its run is as long as the
    /// settings ask; then at each level from the finest down to the 1st order, the high or low it is labelled as, the
    /// Swing it is in and the Trend it is in. The card's checklist, and the order the chapters of an analysis come in.
    /// </summary>
    public static List<Thing> At(List<Price> prices, string time, string? seen, ExplainOptions? options = null, int maxLevel = 8)
    {
        options ??= ExplainOptions.Default;
        var index = Tours.AnchorOf(time, prices);
        var cursor = seen == null ? prices.Count - 1 : Math.Max(index, Tours.AnchorOf(seen, prices));
        var known = cursor < prices.Count - 1 ? prices.GetRange(0, cursor + 1) : prices;
        var things = new List<Thing> { new("Candle", null, "Candle") };
        if (RunAt(known, index, options) is { } run)
            things.Add(new Thing("SuccessiveCandles", null, $"{Capital(Words(run.Length))} Successive {(known[index].Close > known[index].Open ? "Green" : "Red")} Candles"));
        var levels = Sawtooth.Levels(known, Basis, maxLevel + 1);
        var when = known[index].DateTime;
        for (var level = levels.Count - 1; level >= 1; level--)
        {
            if (Read(known, level, levels) is not { } s)
                continue;
            if (LabelAt(s.Points, when) is { } label)
                things.Add(new Thing("Point", level, $"{Order(level)} {label}"));
            if (SwingIn(s, when) is { } swing)
                things.Add(new Thing("Swing", level, $"{Order(level)} {SwingName(swing.Direction)}"));
            if (TrendIn(s, when) is { } trend)
                things.Add(new Thing("Trend", level, $"{Order(level)} {TrendName(trend.Direction)}"));
        }

        return things;
    }

    /// <summary>
    /// A tour of the things ticked at a candle, in the shape of tour.json: a chapter for each, in the order <see cref="At"/>
    /// lists them, from the candle outwards. It starts where the dataset does, so the structure is the Replay page's, and
    /// each chapter jumps to its thing and runs through it. Each thing is the one the chart had at the cursor it was seen
    /// from (seen, the Replay page's; else the end of the prices). Played in place, the chart draws the structure as known
    /// at the cursor a part at a time as the run reaches it, and a Swing or Trend stops on its own last candle; otherwise
    /// a Swing or Trend runs to its own end when the chart had it there too, else on to the cursor. A thing not at the
    /// candle is left out and named in missing. Null when nothing is left.
    /// </summary>
    public static JsonObject? Tour(IReadOnlyList<Thing> things, string dataset, List<Price> prices, string time, string? seen, out List<string> missing, bool inPlace = true, ExplainOptions? options = null)
    {
        options ??= ExplainOptions.Default;
        missing = [];
        if (prices.Count == 0)
            return null;
        var index = Tours.AnchorOf(time, prices);
        var cursor = seen == null ? prices.Count - 1 : Math.Max(index, Tours.AnchorOf(seen, prices));
        var known = cursor < prices.Count - 1 ? prices.GetRange(0, cursor + 1) : prices;
        var sections = new List<JsonObject>();
        int? context = null;
        // From the candle outwards: the Candle, its run, then the finest level's things first.
        static int Rank(Thing thing) => thing.Kind switch { "Candle" => 0, "SuccessiveCandles" => 1, _ => 2 };
        foreach (var thing in things.OrderBy(Rank).ThenByDescending(x => x.Level ?? 0).ThenBy(x => Terms.ToList().IndexOf(x.Kind)))
        {
            var chapter = Chapter(thing, prices, known, index, cursor, inPlace, options, out var keeps);
            if (chapter == null)
            {
                missing.Add(thing.Name);
                continue;
            }

            // The Swing a level coarser that a thing begins inside: the chart keeps the candles from its start, to look back at.
            if (keeps is int k)
                context = Math.Min(context ?? k, k);
            // The thing a chapter tells of, by id, so the page can leave out the chapters of what is unticked.
            chapter["thing"] = thing.Id;
            sections.Add(chapter);
        }

        if (sections.Count == 0)
            return null;
        var when = prices[index].DateTime;
        var one = things.Count == 1 ? things[0] : null;
        var what = one == null ? "Analysis" : $"{(one.Level is int l ? $"{Order(l)} " : "")}{one.Kind}";
        var title = $"{what} at {when:HH:mm} on {when.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}";
        var tour = TourOf(title, dataset, prices[0].DateTime, [Math.Max(0, index - CandlesBefore), index + CandlesAfter], ["level1", "candles"], sections.ToArray());
        if (inPlace)
            tour["known"] = cursor;
        if (context != null)
            tour["context"] = context;
        return tour;
    }

    /// <summary>
    /// A tour of its own around one thing, by its kind and level: see <see cref="Tour(IReadOnlyList{Thing}, string, List{Price}, string, string?, out List{string}, bool, ExplainOptions?)"/>.
    /// Null when the term is not one that can be explained or nothing of the kind is at that candle.
    /// </summary>
    public static JsonObject? Tour(string term, string dataset, List<Price> prices, string time, int level = 1, string? seen = null, bool inPlace = false, ExplainOptions? options = null)
    {
        if (!Terms.Contains(term))
            return null;
        var thing = new Thing(term, term is "Candle" or "SuccessiveCandles" ? null : level, term);
        return Tour([thing], dataset, prices, time, seen, out _, inPlace, options);
    }

    // One chapter of a tour: a section that explains the thing, to be written out, with the layers that draw it and the
    // candle it runs from. Null when the thing is not at the candle. keeps is the candle the chart keeps history from.
    private static JsonObject? Chapter(Thing thing, List<Price> prices, List<Price> known, int index, int cursor, bool inPlace, ExplainOptions options, out int? keeps)
    {
        keeps = null;
        var level = thing.Level ?? 1;
        switch (thing.Kind)
        {
            case "Candle":
                return new JsonObject { ["chapter"] = "Candle", ["explain"] = thing.Kind, ["at"] = index, ["from"] = Math.Max(0, index - CandlesBefore), ["known"] = cursor, ["layers"] = Layers("candles"), ["speed"] = 8.0 };
            case "SuccessiveCandles":
            {
                if (RunAt(known, index, options) is not { } run)
                    return null;
                return new JsonObject { ["chapter"] = "Successive Candles", ["explain"] = thing.Kind, ["at"] = index, ["from"] = Math.Max(0, run.First - 1), ["known"] = cursor, ["runs"] = options.ToJson(), ["layers"] = Layers("candles"), ["speed"] = 8.0 };
            }
            case "Point":
            {
                if (PointAt(known, index, level) is not { } point)
                    return null;
                return new JsonObject { ["chapter"] = $"{Order(level)} {point.Point.Label}", ["explain"] = thing.Kind, ["at"] = index, ["level"] = level, ["from"] = Math.Max(0, point.PreviousAt - 1), ["known"] = cursor, ["layers"] = Layers($"level{level}", "close", "sawtooth"), ["speed"] = 8.0 };
            }
            default:
                return SwingOrTrend(thing.Kind, prices, known, index, level, cursor, inPlace, out keeps);
        }
    }

    // The chapter of a Swing or a Trend: the one at the level that has the candle, as the chart had it at the cursor. The
    // section runs to the thing's own end when the chart has it there too (a Swing's BoS, a Trend's second BoS or the
    // candle), else on to the cursor, as the finer levels can be anchored afresh by the candles between. Played in place,
    // the thing is read at the cursor, as the page showed it, and the run stops on its own last candle whatever the chart
    // had there at the time.
    private static JsonObject? SwingOrTrend(string term, List<Price> prices, List<Price> known, int index, int level, int cursor, bool inPlace, out int? keeps)
    {
        keeps = null;
        // The chart's own layers mark what the texts speak of: the sawtooth, and for a Trend the points that make it.
        var layers = new List<string> { $"level{level}", "close", "sawtooth" };
        int first, until, last;
        string name;
        if (term == "Swing")
        {
            if (SwingAt(known, index, level) is not { } swing)
                return null;
            first = swing.Span.First;
            last = swing.Span.Last;
            until = SwingAt(prices.GetRange(0, last + 1), index, level)?.Span.First == first ? last : cursor;
            name = SwingName(swing.Swing.Direction);
            layers.AddRange(["HigherHigh", "HigherLow", "LowerLow", "LowerHigh"]);
        }
        else
        {
            if (TrendAt(known, index, level) is not { } trend)
                return null;
            first = trend.Start;
            var made = Math.Max(index, trend.Index[trend.Trend.Confirmed.Time]);
            until = TrendAt(prices.GetRange(0, made + 1), index, level)?.Start == first ? made : cursor;
            // Its own last candle: where it ended, or the cursor while it still runs.
            last = trend.Span.Last;
            name = TrendName(trend.Trend.Direction);
            layers.AddRange(trend.Trend.Direction == EnumSwingDirection.Up ? ["HigherHigh", "HigherLow"] : ["LowerLow", "LowerHigh"]);
        }

        var readAt = until;
        if (inPlace)
        {
            readAt = cursor;
            until = last;
            if (level > 1)
                keeps = SwingAt(known, first, level - 1)?.Span.First;
        }

        // The view is the thing and one candle after it: the run begins on its first candle and stops on its last.
        var section = new JsonObject
        {
            ["chapter"] = $"{Order(level)} {name}", ["explain"] = term, ["at"] = index, ["level"] = level, ["from"] = first, ["until"] = until,
            ["view"] = new JsonArray(first, until + 1), ["speed"] = Pace(until - first), ["layers"] = Layers([.. layers]),
        };
        if (inPlace)
            section["known"] = readAt;
        return section;
    }

    private static JsonArray Layers(params string[] layers) => new(layers.Select(x => (JsonNode)x).ToArray());

    private static string SwingName(EnumSwingDirection direction) => direction == EnumSwingDirection.Up ? "Upswing" : "Downswing";

    private static string TrendName(EnumSwingDirection direction) => direction == EnumSwingDirection.Up ? "Uptrend" : "Downtrend";

    /// <summary>
    /// What a written tour of a Trend says of it, for finding others like it: its level, direction, count of Swings and
    /// Strength as the tour shows them, which is as the chart had it where the tour ends. Null when no Trend at that level
    /// has the candle.
    /// </summary>
    public static JsonObject? TrendShown(List<Price> prices, string time, int level = 1, string? seen = null, bool inPlace = false)
    {
        var index = Tours.AnchorOf(time, prices);
        var cursor = seen == null ? prices.Count - 1 : Tours.AnchorOf(seen, prices);
        if (index > cursor)
            return null;
        var known = cursor < prices.Count - 1 ? prices.GetRange(0, cursor + 1) : prices;
        if (TrendAt(known, index, level) is not { } trend)
            return null;
        // Played in place, the tour shows the Trend as it is known at the cursor.
        if (inPlace)
            return TrendRow(trend.Trend, level, trend.Index);
        var made = Math.Max(index, trend.Index[trend.Trend.Confirmed.Time]);
        var until = TrendAt(prices.GetRange(0, made + 1), index, level)?.Start == trend.Start ? made : cursor;
        var shown = TrendAt(prices.GetRange(0, until + 1), index, level) ?? trend;
        return TrendRow(shown.Trend, level, shown.Index);
    }

    /// <summary>
    /// Every Trend the prices have as a whole, at each level up to maxLevel, ordered by the candle it begins at: its level,
    /// direction, count of Swings, Strength, where it begins and ends, the candle a written tour of the whole of it is
    /// opened at, and whether such a tour would show the same Trend.
    /// </summary>
    public static JsonArray AllTrends(List<Price> prices, int maxLevel)
    {
        var rows = new List<JsonObject>();
        for (var level = 1; level <= maxLevel; level++)
        {
            if (Read(prices, level) is not { } s)
                break;
            rows.AddRange(s.Trends.Select(x => TrendRow(x, level, s.Index)));
        }

        // The whole dataset has hindsight the chart did not have at the time, so a tour, which shows the chart as it was,
        // can be written of a Trend only where the chart had the same Trend at its last Swing's BoS.
        Parallel.ForEach(rows, row =>
        {
            var at = Tours.AnchorOf(row["at"]!.GetValue<string>(), prices);
            var then = TrendAt(prices.GetRange(0, at + 1), at, row["level"]!.GetValue<int>());
            row["tour"] = then != null && then.Start == row["index"]!.GetValue<int>() && TrendRow(then.Trend, 0, then.Index) is { } seen
                && seen["direction"]!.GetValue<string>() == row["direction"]!.GetValue<string>()
                && seen["swings"]!.GetValue<int>() == row["swings"]!.GetValue<int>() && seen["strength"]!.GetValue<int>() == row["strength"]!.GetValue<int>();
        });

        return new JsonArray(rows.OrderBy(x => x["index"]!.GetValue<int>()).ThenBy(x => x["level"]!.GetValue<int>()).ToArray<JsonNode>());
    }

    // A Trend as a row to match and list. A tour of the whole of it is opened at its last Swing's BoS, where it has every
    // Swing it will have.
    private static JsonObject TrendRow(TrendOutline trend, int level, Dictionary<DateTime, int> index)
    {
        static string Stamp(DateTime time) => time.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
        return new JsonObject
        {
            ["level"] = level,
            ["direction"] = trend.Direction == EnumSwingDirection.Up ? "Up" : "Down",
            ["swings"] = trend.Swings,
            ["strength"] = trend.Strength,
            ["start"] = Stamp(trend.Start.Time),
            ["index"] = index[trend.Start.Time],
            ["end"] = trend.End != null ? Stamp(trend.End.Time) : null,
            ["at"] = Stamp(trend.Parts.Count > 0 ? trend.Parts[^1].BreakOfStructure.Time : trend.Confirmed.Time),
        };
    }

    private static JsonObject TourOf(string title, string dataset, DateTime start, int[] view, string[] layers, params JsonObject[] sections)
    {
        return new JsonObject
        {
            ["title"] = title,
            ["dataset"] = dataset,
            ["start"] = new JsonObject
            {
                ["time"] = start.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
                ["view"] = new JsonArray(view[0], view[1]),
                ["speed"] = 4.0,
                ["layers"] = new JsonArray(layers.Select(x => (JsonNode)x).ToArray()),
            },
            ["sections"] = new JsonArray(sections.Select(x => (JsonNode)x).ToArray()),
        };
    }

    // Candles a second for a section that runs a stretch: about twenty seconds of replay, between a brisk walk and a sprint.
    private static double Pace(int candles) => Math.Clamp(Math.Round(candles / 20.0), 10, 200);

    private static JsonObject? Write(string term, List<Price> prices, int at, int level, int reached, int? until, int? known, ExplainOptions options, IReadOnlyCollection<string>? blocks, out string problem)
    {
        problem = "";
        switch (term)
        {
            case "Candle":
                return Candle(prices, at, reached, options, known);
            case "SuccessiveCandles":
                return Run(prices, at, reached, options, known, out problem);
            case "Point":
                return Point(prices, at, level, reached, known, out problem);
            case "Swing":
                return Swing(prices, at, level, reached, out problem, until, known, blocks);
            default:
                return Trend(prices, at, level, reached, out problem, until, known, blocks);
        }
    }

    /// <summary>
    /// The written parts of a section that explains one candle, a block for each term in it. The Candle: when it is and
    /// its four prices, pinned to it, with what a candle is at Education. Its colour: why it is green or red, with the
    /// nearest candle of the other colour for contrast. The view has the candle a little right of centre; until is set
    /// when the replay has not reached it. The run it may be in is a thing of its own (see <see cref="Run"/>).
    /// </summary>
    public static JsonObject Candle(List<Price> prices, int at, int reached, ExplainOptions? options = null, int? known = null)
    {
        var p = prices[at];
        var length = Length(prices);
        var unit = Unit(length);
        var up = p.Close > p.Open;
        var flat = p.Close == p.Open;
        // The candles there are to read: those the replay has drawn, or up to known, the cursor the candle was seen from.
        var drawn = Math.Max(Math.Max(at, reached), known ?? 0);
        var view = new[] { Math.Max(0, at - CandlesBefore), at + CandlesAfter };
        var layers = new List<string> { "candles" };

        // Every text waits for the candle, so the replay draws it before anything is said, whether or not it was there already.
        // The Candle: what one is, then this one.
        var cues = new JsonArray
        {
            Teach(at, Definitions.Text("Candle.what", period: unit)),
            Say(at, $"{Capital(Span(p.DateTime, length))}."),
            Teach(at, Definitions.Text("Candle.body", period: unit)),
            Say(at, $"Open {Money(p.Open)}, close {Money(p.Close)}.", hold: 0),
            Pin(at, "open", "left", $"open: {Money(p.Open)}", 4),
            Pin(at, "close", "right", $"close: {Money(p.Close)}", 4),
            Teach(at, Definitions.Text("Candle.wicks", period: unit)),
            Say(at, $"High {Money(p.High)}, low {Money(p.Low)}.", hold: 0),
            Pin(at, "high", "above", $"high: {Money(p.High)}", 2.5),
            Pin(at, "low", "below", $"low: {Money(p.Low)}", 2.5),
        };

        // Its colour: the rule, then this one; at Education, the nearest drawn candle in view of the other colour, the
        // one before preferred, set against it.
        cues.Add(Teach(at, Definitions.Text("Candle.colour")));
        cues.Add(Say(at, flat ? "No body: it closed where it opened." : up ? "Green: it closed above its open." : "Red: it closed below its open."));
        if (!flat)
        {
            var other = Enumerable.Range(1, CandlesBefore + CandlesAfter)
                .SelectMany(d => new[] { at - d, at + d })
                .Where(i => i >= view[0] && i <= Math.Min(drawn, view[1]) && i != at)
                .FirstOrDefault(i => up ? prices[i].Close < prices[i].Open : prices[i].Close > prices[i].Open, -1);
            if (other >= 0)
            {
                var colour = up ? "red" : "green";
                cues.Add(Teach(at, $"{Which(other - at)} closed {(up ? "lower" : "higher")} than the open, so it is {colour}."));
                cues.Add(Pin(other, up ? "low" : "high", up ? "below" : "above", colour, 3.5, EnumDetail.Education));
            }
        }

        return Section(view, at > reached ? at : null, layers, cues);
    }

    /// <summary>
    /// The written parts of a section that explains the Successive Candles a candle is in: the run of its colour, as drawn
    /// so far or up to known, when it is as long as the settings ask. What Successive Candles are at Education, then which
    /// of the run the candle is and how far the run goes, told on the run's last candle, which the replay runs to. Null,
    /// with the problem, when the candle is in no such run.
    /// </summary>
    public static JsonObject? Run(List<Price> prices, int at, int reached, ExplainOptions? options, int? known, out string problem)
    {
        options ??= ExplainOptions.Default;
        var drawn = Math.Max(Math.Max(at, reached), known ?? 0);
        var p = prices[at];
        if (p.Close == p.Open || RunAt(prices.GetRange(0, drawn + 1), at, options) is not { } run)
        {
            problem = $"#{at} is not in a run of candles of one colour as long as the setting";
            return null;
        }

        var up = p.Close > p.Open;
        var length = Length(prices);
        var colour = up ? "green" : "red";
        var term = up ? "Successive Green Candles" : "Successive Red Candles";
        var cues = new JsonArray
        {
            Teach(run.Last, Definitions.Text("SuccessiveCandles.what")),
            Say(run.Last, $"{Capital(Ordinal(run.Position))} of {Words(run.Length)} {term}, {Clock(prices[run.First].DateTime, length)} to {Clock(prices[run.Last].DateTime, length)}."),
        };
        problem = "";
        return Section([Math.Max(0, run.First - 2), run.Last + 2], run.Last > reached ? run.Last : null, ["candles", up ? "SuccessiveGreenCandles" : "SuccessiveRedCandles"], cues);
    }

    // A candle's place in a run of its colour: which it is, how long the run is and where the run begins and ends.
    private sealed record CandleRun(int Position, int Length, int First, int Last);

    // The run of one colour that has the candle, when it is as long as the page's setting for that colour.
    private static CandleRun? RunAt(List<Price> prices, int at, ExplainOptions options)
    {
        var run = CandleRuns.Runs(prices, minLength: 2).FirstOrDefault(x => x.Start.Time <= prices[at].DateTime && x.End.Time >= prices[at].DateTime);
        if (run == null || run.Length < (run.Green ? options.GreenRuns : options.RedRuns))
            return null;
        var first = prices.FindIndex(x => x.DateTime == run.Start.Time);
        return new CandleRun(at - first + 1, run.Length, first, first + run.Length - 1);
    }

    /// <summary>
    /// The written parts of a section that explains the high or low a candle is labelled as at a level, as known at the
    /// chart's cursor: what the label means at Education, then this close against the high or low before it of the same
    /// kind, both pinned, and whether it can still move. Null, with the problem, when the candle is not a labelled high or
    /// low at that level.
    /// </summary>
    public static JsonObject? Point(List<Price> prices, int at, int level, int reached, int? known, out string problem)
    {
        var drawn = Math.Max(Math.Max(at, reached), known ?? 0);
        if (PointAt(prices.GetRange(0, drawn + 1), at, level) is not { } found)
        {
            problem = $"#{at} is not a labelled high or low of the {Order(level)}";
            return null;
        }

        var (point, previous, previousAt) = (found.Point, found.Previous, found.PreviousAt);
        var length = Length(prices);
        var high = point.Kind == EnumPivotKind.High;
        var kind = high ? "high" : "low";
        var label = point.Label;
        var more = label is "HH" or "HL";
        var place = high ? "above" : "below";
        var cues = new JsonArray
        {
            Teach(at, Definitions.Text($"Point.{label}")),
            Teach(at, Definitions.Text("Point.closes")),
            Say(at, previous == null
                ? $"{Order(level)} {label} at {Money(point.Price)}."
                : $"{Order(level)} {label} at {Money(point.Price)}, {(more ? "above" : "below")} the {kind} of {Money(previous.Price)} at {Clock(previous.Time, length)}."),
            Pin(at, "close", place, $"{label}: {Money(point.Price)}", 4),
        };
        if (previous != null)
            cues.Add(Pin(previousAt, "close", Edge(previousAt, place), $"{(previous.Label.Length > 0 ? previous.Label : kind)}: {Money(previous.Price)}", 4));
        if (point.Provisional)
        {
            cues.Add(Teach(at, Definitions.Text("Point.moving")));
            cues.Add(Say(at, "Still moving."));
        }
        problem = "";
        return Section([Math.Max(0, previousAt - 2), at + 2], at > reached ? at : null, [$"level{level}", "close", "sawtooth", PointLayer(label)], cues, byCandle: true);
    }

    private sealed record FoundPoint(StructurePoint Point, StructurePoint? Previous, int PreviousAt);

    // The labelled high or low at a level that the candle is, with the one of its kind before it, which it is read against.
    private static FoundPoint? PointAt(List<Price> prices, int at, int level)
    {
        if (Read(prices, level) is not { } s)
            return null;
        var time = prices[at].DateTime;
        var point = s.Points.FirstOrDefault(x => x.Time == time && x.Type != null && x.Label.Length > 0);
        if (point == null)
            return null;
        var previous = s.Points.LastOrDefault(x => x.Kind == point.Kind && x.Time < point.Time);
        return new FoundPoint(point, previous, previous != null && s.Index.TryGetValue(previous.Time, out var i) ? i : at);
    }

    /// <summary>
    /// The written parts of a section that explains the Swing at a level that a candle is in, as the chart has it where the
    /// section ends: at until when the section says so, else once the Swing's BoS is drawn. Where it begins and at what
    /// kind of high or low, the fall or rise to its Swing Low or High, an MSB on the way when there is one, the BoS that
    /// Confirms it, its two legs, and whether it is Strong or Weak. Null, with the problem, when no Swing at that level has
    /// the candle.
    /// </summary>
    public static JsonObject? Swing(List<Price> prices, int at, int level, int reached, out string problem, int? runsTo = null, int? known = null, IReadOnlyCollection<string>? blocks = null)
    {
        FoundSwing? seen;
        int end;
        if (runsTo is int given)
        {
            end = Math.Max(reached, given);
            seen = SwingAt(prices.GetRange(0, Math.Max(end, known ?? end) + 1), at, level);
            if (seen == null)
            {
                problem = $"no {Order(level)} Swing on the chart at #{end} has #{at} in it";
                return null;
            }
        }
        else
        {
            var found = SwingAt(prices, at, level);
            if (found == null)
            {
                problem = $"no {Order(level)} Swing has #{at} in it";
                return null;
            }

            end = Math.Max(reached, found.Span.Last);
            seen = SwingAt(prices.GetRange(0, end + 1), found.Span.First, level, found.Swing.Direction);
            if (seen == null)
            {
                problem = $"the {Order(level)} Swing that begins at #{found.Span.First} is not on the chart at #{end}";
                return null;
            }
        }

        var (swing, span, index) = (seen.Swing, seen.Span, seen.Index);
        var against = swing.Direction == EnumSwingDirection.Up ? EnumAnnotationType.BearishMarketStructureBreak : EnumAnnotationType.BullishMarketStructureBreak;
        var msb = seen.Breaks.FirstOrDefault(b => b.Type == against && b.Break.Time > swing.Start.Time && b.Break.Time < swing.BreakOfStructure!.Time);
        var s = new SwingRead(prices, swing, level, span.First, index[swing.Extreme.Time], span.Last,
            LabelAt(seen.Points, swing.Start.Time), LabelAt(seen.Points, swing.Extreme.Time), msb != null ? index[msb.Break.Time] : null);

        // A block for each term a Swing is made of, each where it shows on this one; the MSB's only when it has one. The
        // view is the Swing, and on to where the replay runs when it runs on past the BoS.
        problem = "";
        return Section([Math.Max(0, span.First - Margin), Math.Max(s.BosAt, end > reached ? end : s.BosAt) + Margin], end > reached ? end : null,
            SwingBlocks.Where(x => blocks == null || blocks.Contains(x.Name)).Select(x => x.Tell(s)).ToArray());
    }

    // A Swing as the chart has it, read once for its blocks: where it begins, turns and breaks structure, the labels of
    // its high and low, and its MSB, when it has one.
    private sealed record SwingRead(List<Price> Prices, SwingOutline Swing, int Level, int First, int ExtremeAt, int BosAt, string? StartLabel, string? TurnLabel, int? MsbAt)
    {
        public EnumSwingDirection Direction => Swing.Direction;
        public bool Up => Direction == EnumSwingDirection.Up;
        public string Name => SwingName(Direction);
        public string Turn => Up ? "Swing Low" : "Swing High";
        public TimeSpan Period => Length(Prices);
    }

    // The Swing: what one is, then where this one begins and at what kind of high or low.
    private static Block SwingBegins(SwingRead s)
    {
        var kind = s.Up ? "high" : "low";
        return new Block(
        [
            Teach(s.First, Definitions.Text("Swing.what", s.Direction)),
            Say(s.First, $"{Order(s.Level)} {s.Name}, from {Named(s.StartLabel, kind)} at {Money(s.Swing.Start.Price)}, {At(s.Swing.Start.Time, s.Period)}."),
            Pin(s.First, "close", Edge(s.First, s.Up ? "above" : "below"), $"{s.StartLabel ?? kind}: {Money(s.Swing.Start.Price)}", 4),
        ], [$"level{s.Level}", s.Name, .. Labelled(s.StartLabel)]);
    }

    // Its Swing Low or Swing High: what the turn is, then this one and what kind of low or high it is.
    private static Block SwingTurn(SwingRead s) => new(
    [
        Teach(s.ExtremeAt, Definitions.Text("Swing.turn", s.Direction)),
        Say(s.ExtremeAt, $"{s.Turn}: {Named(s.TurnLabel, s.Up ? "low" : "high")} at {Money(s.Swing.Extreme.Price)}."),
        Pin(s.ExtremeAt, "close", s.Up ? "below" : "above", $"{s.Turn}: {Money(s.Swing.Extreme.Price)}", 4),
    ], Labelled(s.TurnLabel));

    // The MSB inside it, when it has one: what an MSB is and how it goes against the Swing, then this one.
    private static Block SwingMsb(SwingRead s)
    {
        if (s.MsbAt is not int at)
            return Block.None;
        return new Block(
        [
            Teach(at, Definitions.Text("Swing.msb", s.Direction)),
            Teach(at, Definitions.Text("Swing.against", s.Direction)),
            Say(at, $"MSB at {Money(s.Prices[at].Close)}, {(s.Up ? "below" : "above")} the {s.Turn} of the {s.Name} before."),
            Pin(at, "close", "left", "MSB", 3.5),
        ], [s.Up ? "BearishMarketStructureBreak" : "BullishMarketStructureBreak", s.Up ? "msbLevelDown" : "msbLevelUp"]);
    }

    // Its BoS: what a BoS is and that it Confirms the Swing, then this one.
    private static Block SwingBos(SwingRead s) => new(
    [
        Teach(s.BosAt, Definitions.Text("Swing.bos", s.Direction)),
        Teach(s.BosAt, Definitions.Text("Swing.candidate", s.Direction)),
        Say(s.BosAt, $"BoS at {Money(s.Swing.BreakOfStructure!.Price)}."),
        Pin(s.BosAt, "close", s.Up ? "above" : "below", "BoS", 4),
    ], [s.Up ? "BullishBreakOfStructure" : "BearishBreakOfStructure", s.Up ? "bosLevelUp" : "bosLevelDown"]);

    // Its two legs: what they are, then how long each of this one's is.
    private static Block SwingLegs(SwingRead s)
    {
        var (first, second) = s.Up ? ("Downleg", "Upleg") : ("Upleg", "Downleg");
        return new Block(
        [
            Teach(s.BosAt, Definitions.Text("Swing.legs", s.Direction)),
            Say(s.BosAt, $"The {first} is {Duration(s.ExtremeAt - s.First, s.Period)}; the {second} is {Duration(s.BosAt - s.ExtremeAt, s.Period)}."),
        ], []);
    }

    // Strong or Weak: the rule, the Weak one only when it has an MSB, then which this one is.
    private static Block SwingStrength(SwingRead s)
    {
        var weak = s.MsbAt != null;
        List<JsonObject> cues = [Teach(s.BosAt, Definitions.Text("Swing.strong", s.Direction))];
        if (weak)
            cues.Add(Teach(s.BosAt, Definitions.Text("Swing.weak", s.Direction)));
        cues.Add(Say(s.BosAt, weak ? "Weak: it has an MSB." : "Strong: no MSB."));
        return new Block(cues, []);
    }

    /// <summary>
    /// The written parts of a section that explains the Trend at a level that a candle is in, as the chart has it where
    /// the section ends: at until when the section says so, else at the candle, or where the second BoS makes it a Trend.
    /// Where it begins, that second BoS, its count, which of its Swings are Weak, its Strength, and whether it has ended at
    /// a Swing the other way or still runs. Null, with the problem, when no Trend at that level has the candle.
    /// </summary>
    public static JsonObject? Trend(List<Price> prices, int at, int level, int reached, out string problem, int? runsTo = null, int? known = null, IReadOnlyCollection<string>? blocks = null)
    {
        FoundTrend? seen;
        int end;
        if (runsTo is int given)
        {
            end = Math.Max(reached, given);
            seen = TrendAt(prices.GetRange(0, Math.Max(end, known ?? end) + 1), at, level);
            if (seen == null)
            {
                problem = $"no {Order(level)} Trend on the chart at #{end} has #{at} in it";
                return null;
            }
        }
        else
        {
            var found = TrendAt(prices, at, level);
            if (found == null)
            {
                problem = $"no {Order(level)} Trend has #{at} in it";
                return null;
            }

            end = Math.Max(Math.Max(reached, at), found.Index[found.Trend.Confirmed.Time]);
            seen = TrendAt(prices.GetRange(0, end + 1), found.Start, level, found.Trend.Direction);
            if (seen == null)
            {
                problem = $"the {Order(level)} Trend that begins at #{found.Start} is not on the chart at #{end}";
                return null;
            }
        }

        var (trend, index) = (seen.Trend, seen.Index);
        var weak = trend.Parts.Select((part, i) => (part, i)).Where(x => !x.part.Strong).Select(x => Ordinal(x.i + 1)).ToList();
        var endedAt = trend.End != null && index.TryGetValue(trend.End.Time, out var e) && e <= end ? e : (int?)null;
        var t = new TrendRead(prices, trend, level, seen.Start, index[trend.Confirmed.Time], end, endedAt, weak);

        // A block for each term a Trend is made of, each where it shows on this one; the rules about Weak Swings only
        // when one of its Swings is Weak.
        problem = "";
        return Section([Math.Max(0, t.StartAt - Margin), end + Margin], end > reached ? end : null,
            TrendBlocks.Where(x => blocks == null || blocks.Contains(x.Name)).Select(x => x.Tell(t)).ToArray());
    }

    // The blocks a Swing is told in, by name, in the order they are told.
    private static readonly (string Name, Func<SwingRead, Block> Tell)[] SwingBlocks =
        [("Begins", SwingBegins), ("Turn", SwingTurn), ("Msb", SwingMsb), ("Bos", SwingBos), ("Legs", SwingLegs), ("Strength", SwingStrength)];

    // The blocks a Trend is told in, by name, in the order they are told.
    private static readonly (string Name, Func<TrendRead, Block> Tell)[] TrendBlocks =
        [("Begins", TrendBegins), ("Confirmed", TrendConfirmed), ("Swings", TrendSwings), ("Weak", TrendWeak), ("Strength", TrendStrength), ("Ends", TrendEnds), ("IsACount", TrendIsACount)];

    // A Trend as the chart has it at until, read once for its blocks: where it begins and is Confirmed, which of its
    // Swings are Weak (as ordinals), and where it ended, when it has by until.
    private sealed record TrendRead(List<Price> Prices, TrendOutline Trend, int Level, int StartAt, int ConfirmedAt, int Until, int? EndedAt, IReadOnlyList<string> Weak)
    {
        public EnumSwingDirection Direction => Trend.Direction;
        public bool Up => Direction == EnumSwingDirection.Up;
        public string Name => TrendName(Direction);
        public string Swing => SwingName(Direction);
        public string Other => SwingName(Up ? EnumSwingDirection.Down : EnumSwingDirection.Up);
        public string Place => Up ? "above" : "below";
        public TimeSpan Period => Length(Prices);
    }

    // The Trend: what one is, then where this one begins.
    private static Block TrendBegins(TrendRead t) => new(
    [
        Teach(t.StartAt, Definitions.Text("Trend.what", t.Direction)),
        Say(t.StartAt, $"{Order(t.Level)} {t.Name}, beginning {At(t.Trend.Start.Time, t.Period)}."),
        Pin(t.StartAt, "close", Edge(t.StartAt, t.Place), $"{t.Name} begins", 4),
    ], [$"level{t.Level}", t.Swing, t.Name, "trendArrows"]);

    // The second Swing's BoS: what Confirms a Trend, then where this one was.
    private static Block TrendConfirmed(TrendRead t) => new(
    [
        Teach(t.ConfirmedAt, Definitions.Text("Trend.confirmed", t.Direction)),
        Say(t.ConfirmedAt, $"Confirmed at the second {t.Swing}'s BoS, {At(t.Trend.Confirmed.Time, t.Period)}."),
        Pin(t.ConfirmedAt, "close", t.Place, $"second {t.Swing}", 4),
    ], [t.Up ? "BullishBreakOfStructure" : "BearishBreakOfStructure"]);

    // How many Swings it has.
    private static Block TrendSwings(TrendRead t) => new(
    [
        Say(t.Until, $"{Capital(Words(t.Trend.Swings))} {t.Swing}s{(t.EndedAt != null ? "" : " so far")}."),
    ], []);

    // Its Weak Swings: when it has any, what makes a Swing Weak and what an MSB against a Trend leads to; then which.
    private static Block TrendWeak(TrendRead t)
    {
        List<JsonObject> cues = [];
        if (t.Weak.Count > 0)
            cues.AddRange([
                Teach(t.Until, Definitions.Text("Swing.strong", t.Direction)),
                Teach(t.Until, Definitions.Text("Swing.weak", t.Direction)),
                Teach(t.Until, Definitions.Text("Trend.next", t.Direction)),
            ]);
        cues.Add(Say(t.Until, t.Weak.Count == 0 ? "All Strong." : $"{Capital(Words(t.Weak.Count))} Weak: the {List(t.Weak)}."));
        return new Block(cues, t.Weak.Count > 0 ? [t.Up ? "BearishMarketStructureBreak" : "BullishMarketStructureBreak"] : []);
    }

    // Its Strength: what it is, then this one's.
    private static Block TrendStrength(TrendRead t) => new(
    [
        Teach(t.Until, Definitions.Text("Trend.strength", t.Direction)),
        Say(t.Until, $"Strength {t.Trend.Strength}%: {Words(t.Trend.Strong)} of {Words(t.Trend.Swings)}."),
    ], []);

    // Its end: what ends a Trend, then where this one ended, or that it still runs.
    private static Block TrendEnds(TrendRead t)
    {
        if (t.EndedAt is not int at)
            return new Block(
            [
                Teach(t.Until, Definitions.Text("Trend.ends", t.Direction)),
                Say(t.Until, $"Still running: no {t.Other} Confirmed since."),
            ], []);
        return new Block(
        [
            Teach(t.Until, Definitions.Text("Trend.ends", t.Direction)),
            Say(t.Until, $"Ended at the first {t.Other}'s BoS, {At(t.Trend.End!.Time, t.Period)}."),
            Pin(at, "close", t.Up ? "below" : "above", $"{t.Name} ends", 4),
        ], [t.Other]);
    }

    // What a Trend's count claims: nothing about what the price does next.
    private static Block TrendIsACount(TrendRead t) => new([Teach(t.Until, Definitions.Text("Trend.count", t.Direction))], []);

    // What a section tells of one term: its cues, and the layers that draw what they speak of.
    private sealed record Block(IReadOnlyList<JsonObject> Cues, IReadOnlyList<string> Layers)
    {
        public static readonly Block None = new([], []);
    }

    // The page's layer for a point's label, when it has one.
    private static string[] Labelled(string? label) => label != null ? [PointLayer(label)] : [];

    // What the structure at a level has, read from these prices alone.
    private sealed record Structure(List<SwingOutline> Swings, List<MarketStructureBreakOutline> Breaks, List<TrendOutline> Trends, List<StructurePoint> Points, Dictionary<DateTime, int> Index);

    private sealed record FoundSwing(SwingOutline Swing, (int First, int Last) Span, List<MarketStructureBreakOutline> Breaks, List<StructurePoint> Points, Dictionary<DateTime, int> Index);

    private sealed record FoundTrend(TrendOutline Trend, int Start, (int First, int Last) Span, Dictionary<DateTime, int> Index);

    private static Structure? Read(List<Price> prices, int level, IReadOnlyList<SawtoothLevel>? levels = null)
    {
        levels ??= Sawtooth.Levels(prices, Basis, level + 1);
        if (level < 1 || level >= levels.Count)
            return null;
        var swings = Sawtooth.Swings(prices, levels, Basis, level).OrderBy(x => x.Start.Time).ToList();
        var breaks = Sawtooth.MarketStructureBreaks(prices, swings, Basis);
        var index = new Dictionary<DateTime, int>();
        for (var i = 0; i < prices.Count; i++)
            index.TryAdd(prices[i].DateTime, i);
        return new Structure(swings, breaks, Sawtooth.Trends(swings, breaks), Sawtooth.Points(prices, levels[level], Basis), index);
    }

    // The Confirmed Swing at a level that has the candle: one that begins there, else the one it falls inside, from its
    // start to its BoS. With a direction, only a Swing beginning at the candle that runs that way.
    private static FoundSwing? SwingAt(List<Price> prices, int at, int level, EnumSwingDirection? direction = null)
    {
        if (Read(prices, level) is not { } s)
            return null;
        var swing = SwingIn(s, prices[at].DateTime, direction);
        return swing == null ? null : new FoundSwing(swing, (s.Index[swing.Start.Time], s.Index[swing.BreakOfStructure!.Time]), s.Breaks, s.Points, s.Index);
    }

    private static SwingOutline? SwingIn(Structure s, DateTime time, EnumSwingDirection? direction = null) => direction != null
        ? s.Swings.FirstOrDefault(x => x.Start.Time == time && x.Direction == direction && x.BreakOfStructure != null)
        : s.Swings.Where(x => x.BreakOfStructure != null).LastOrDefault(x => x.Start.Time <= time && x.BreakOfStructure!.Time >= time);

    private static TrendOutline? TrendIn(Structure s, DateTime time, EnumSwingDirection? direction = null) => direction != null
        ? s.Trends.FirstOrDefault(x => x.Start.Time == time && x.Direction == direction)
        : s.Trends.LastOrDefault(x => x.Start.Time <= time && (x.End == null || x.End.Time >= time));

    // The Trend at a level that has the candle: begun at or before it and not ended before it. With a direction, only a
    // Trend beginning at the candle that runs that way.
    private static FoundTrend? TrendAt(List<Price> prices, int at, int level, EnumSwingDirection? direction = null)
    {
        if (Read(prices, level) is not { } s)
            return null;
        var trend = TrendIn(s, prices[at].DateTime, direction);
        if (trend == null)
            return null;
        var start = s.Index[trend.Start.Time];
        var last = trend.End != null && s.Index.TryGetValue(trend.End.Time, out var end) ? end : Math.Max(s.Index[trend.Confirmed.Time], prices.Count - 1);
        return new FoundTrend(trend, start, (start, last), s.Index);
    }

    // A section that runs through what it explains lists its texts in the order they are shown: by their candle, and as
    // written among those at the same candle.
    private static JsonObject Section(int[] view, int? until, IEnumerable<string> layers, JsonArray cues, bool byCandle = false)
    {
        var section = new JsonObject { ["view"] = new JsonArray(view[0], view[1]), ["add"] = new JsonArray(layers.Distinct().Select(x => (JsonNode)x).ToArray()) };
        if (until != null)
            section["until"] = until;
        if (byCandle)
        {
            var ordered = cues.ToList().OrderBy(x => x!["at"]!.GetValue<int>()).ToList();
            cues.Clear();
            cues = new JsonArray(ordered.ToArray());
        }
        section["cues"] = cues;
        return section;
    }

    // A section told in blocks, a term at a time: their cues by candle, and every layer any of them draws.
    private static JsonObject Section(int[] view, int? until, params Block[] blocks) =>
        Section(view, until, blocks.SelectMany(x => x.Layers), new JsonArray(blocks.SelectMany(x => x.Cues).ToArray<JsonNode>()), byCandle: true);

    // A text of this instance: shown at every level of detail.
    private static JsonObject Say(int at, string summary, double? hold = null)
    {
        var cue = new JsonObject { ["at"] = at, ["texts"] = Texts((EnumDetail.Summary, summary)) };
        if (hold != null)
            cue["hold"] = hold;
        return cue;
    }

    // A text about things of this kind: shown at Education only.
    private static JsonObject Teach(int at, string education) => new() { ["at"] = at, ["texts"] = Texts((EnumDetail.Education, education)) };

    // A label pinned to a point of a candle: shown, not read. At Summary unless given, as it marks this instance.
    private static JsonObject Pin(int at, string on, string place, string text, double hold, EnumDetail detail = EnumDetail.Summary) =>
        new() { ["at"] = at, ["on"] = on, ["place"] = place, ["texts"] = Texts((detail, text)), ["hold"] = hold, ["voice"] = false };

    private static JsonObject Texts(params (EnumDetail Level, string Text)[] texts) => Details.ToJson(texts.ToDictionary(x => x.Level, x => x.Text));

    // A pin on a candle at the chart's left edge goes to the right of it, where there is room for it.
    private static string Edge(int at, string place) => at < Margin ? "right" : place;

    private static TimeSpan Length(IReadOnlyList<Price> prices) => prices.Count > 1 ? prices[1].DateTime - prices[0].DateTime : TimeSpan.FromHours(1);

    // What a candle spans, as a word: "hour", "four hours", "day".
    private static string Unit(TimeSpan length)
    {
        if (length >= TimeSpan.FromDays(1))
            return "day";
        var hours = (int)Math.Round(length.TotalHours);
        return hours == 1 ? "hour" : $"{Words(hours)} hours";
    }

    // A candle's time as the tour says it in passing: its clock time, or its day for daily candles.
    private static string Clock(DateTime time, TimeSpan length) =>
        length >= TimeSpan.FromDays(1) ? time.ToString("d MMMM", CultureInfo.InvariantCulture) : time.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string? LabelAt(List<StructurePoint> points, DateTime time) =>
        points.FirstOrDefault(x => x.Time == time && x.Type != null && x.Label.Length > 0)?.Label;

    // The page's layer for a point's label.
    private static string PointLayer(string label) => label switch { "HH" => "HigherHigh", "HL" => "HigherLow", "LL" => "LowerLow", _ => "LowerHigh" };

    // "an HH", or the plain word when the point has no label.
    private static string Named(string? label, string plain) => label != null ? $"an {label}" : $"a {plain}";

    private static string An(string word) => $"{("aeiou".Contains(char.ToLowerInvariant(word[0])) || word.StartsWith('8') || word.StartsWith("11") || word.StartsWith("18") ? "an" : "a")} {word}";

    // A price as the tour writes them: rounded down, no decimals.
    private static string Money(double price) => Math.Floor(price).ToString("0", CultureInfo.InvariantCulture);

    // "1st order", "2nd order": a sawtooth level as the tour names it.
    private static string Order(int level) => $"{level}{(level % 10 == 1 && level != 11 ? "st" : level % 10 == 2 && level != 12 ? "nd" : level % 10 == 3 && level != 13 ? "rd" : "th")} order";

    // "the hour from 00:00 to 01:00 on Tuesday 3 January 2023", or the day for daily candles.
    private static string Span(DateTime start, TimeSpan length)
    {
        var day = start.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture);
        if (length >= TimeSpan.FromDays(1))
            return $"the day of {day}";
        var hours = (int)Math.Round(length.TotalHours);
        var span = hours == 1 ? "hour" : $"{Words(hours)} hours";
        return $"the {span} from {start:HH:mm} to {start + length:HH:mm} on {day}";
    }

    // "at 02:00 on Tuesday 3 January 2023", or just the day for daily candles.
    private static string At(DateTime time, TimeSpan length)
    {
        var day = time.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture);
        return length >= TimeSpan.FromDays(1) ? $"on {day}" : $"at {time:HH:mm} on {day}";
    }

    // A number of candles as a time: "an hour", "three hours", "eleven days", "two weeks".
    private static string Duration(int candles, TimeSpan length)
    {
        var hours = (int)Math.Round(candles * length.TotalHours);
        if (hours < 1)
            return "less than an hour";
        if (hours == 1)
            return "an hour";
        if (hours < 48)
            return $"{Words(hours)} hours";
        var days = hours / 24;
        if (days < 28)
            return $"{Words(days)} days";
        return days / 7 == 1 ? "a week" : $"{Words(days / 7)} weeks";
    }

    private static string Words(int n) => n switch
    {
        < 0 => n.ToString(CultureInfo.InvariantCulture),
        < 20 => n == 0 ? "no" : Units[n],
        < 100 => n % 10 == 0 ? Tens[n / 10] : $"{Tens[n / 10]}-{Units[n % 10]}",
        _ => n.ToString(CultureInfo.InvariantCulture),
    };

    private static string Ordinal(int n) => n switch
    {
        < 1 => n.ToString(CultureInfo.InvariantCulture),
        < 20 => UnitOrdinals[n],
        < 100 => n % 10 == 0 ? Tens[n / 10][..^1] + "ieth" : $"{Tens[n / 10]}-{UnitOrdinals[n % 10]}",
        _ => $"{n}th",
    };

    private static string List(IReadOnlyList<string> items) =>
        items.Count == 1 ? items[0] : $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}";

    // Another candle by where it is from the one explained.
    private static string Which(int offset) => offset switch
    {
        -1 => "The candle before it",
        1 => "The candle after it",
        < 0 => $"{Capital(Words(-offset))} candles earlier, the candle",
        _ => $"{Capital(Words(offset))} candles later, the candle",
    };

    private static string Capital(string word) => char.ToUpperInvariant(word[0]) + word[1..];
}
