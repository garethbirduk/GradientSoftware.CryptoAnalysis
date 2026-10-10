using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Gradient.CryptoAnalysis.Site;

/// <summary>
/// How much a text tells, from least to most. A cue has a text for each level it is written at and shows the one for the
/// highest level at or below the page's setting; a cue with none that low is left out. Summary is what this instance is;
/// Education is what a thing of its kind is and how it works.
/// </summary>
public enum EnumDetail { Summary, Education }

/// <summary>
/// One text of a tour section, as its texts by level of detail. At is the candle it appears at (null: as the section
/// begins), On what it is pinned to. A line runs from the pinned point to LineTo, the candle and point it ends on, and
/// appears when the replay reaches that. Text is the text at the highest level the cue has: what the facts and tests see.
/// </summary>
public sealed record TourCue(int Index, IReadOnlyDictionary<EnumDetail, string> Texts, int? At, string? On, string? Place, string Keep, double Hold, (int At, string On)? LineTo = null)
{
    public string Text => Texts.Count == 0 ? "" : Texts[Texts.Keys.Max()];
}

/// <summary>
/// The names of the levels of detail as the page and tour.json write them: "summary", "education".
/// </summary>
public static class Details
{
    public static string Name(EnumDetail detail) => detail.ToString().ToLowerInvariant();

    public static EnumDetail? Parse(string name) => Enum.GetValues<EnumDetail>().Cast<EnumDetail?>().FirstOrDefault(x => Name(x!.Value) == name);

    /// <summary>
    /// A cue's texts as the page reads them: { "summary": "…", "education": "…" }.
    /// </summary>
    public static JsonObject ToJson(IReadOnlyDictionary<EnumDetail, string> texts)
    {
        var json = new JsonObject();
        foreach (var (level, text) in texts.OrderBy(x => x.Key))
            json[Name(level)] = text;
        return json;
    }
}

/// <summary>
/// A tour section with everything worked out: the scene it is in, the layers on, the sawtooth levels looked at, the candles
/// it runs from and to. Journey names the fast forward or rewind that runs the replay to From, when the section has one.
/// </summary>
public sealed record TourSection(
    int Index,
    string? Chapter,
    int Scene,
    IReadOnlyList<string> Layers,
    IReadOnlyList<int> Levels,
    int From,
    int Until,
    IReadOnlyList<int> View,
    double Speed,
    IReadOnlyList<TourCue> Cues,
    string? Journey = null);

/// <summary>
/// One scene of a tour: a replay of a dataset from the candle its start time lands on, with its candles numbered from #0.
/// The tour's start state is the first scene; a section with "scene" begins another.
/// </summary>
public sealed record TourScene(string Dataset, int Anchor);

/// <summary>
/// A tour as compiled from tour.json against its datasets: its scenes, its sections, and whatever the file gets wrong.
/// Anchor is the first scene's, the candle the tour's start time lands on. Expanded is the file with every section that
/// explains something written out (see <see cref="Explain"/>), so a tool that reads the texts sees those too.
/// </summary>
public sealed record TourScript(string? Title, string? Dataset, int Anchor, IReadOnlyList<TourScene> Scenes, IReadOnlyList<TourSection> Sections, IReadOnlyList<string> Errors,
    JsonNode? Expanded = null);

/// <summary>
/// Reads the page's tour.json the way the page does (see the Tour section of index.html), so a test can check the file and the
/// structure it describes, and the server can resolve the events a tour refers to.
/// A candle in the file is a number counted from the tour's start, or an event to look for from a candle on:
/// { "event": "BullishBreakOfStructure", "level": 1, "nth": 1 }, the nth time that becomes known at that level.
/// A section with "explain" has its texts written from the prices (see <see cref="Explain"/>). A text is given for each
/// level of detail it is written at: { "texts": { "summary": "…", "education": "…" } }.
/// </summary>
public static class Tours
{
    /// <summary>
    /// The layers the page draws that are not terms.
    /// </summary>
    public static readonly IReadOnlySet<string> PageLayers = new HashSet<string>(
    [
        "close", "price", "sawtooth", "bosLevelUp", "bosLevelDown", "msbLevelUp", "msbLevelDown", "trendArrows", "rangeBands", "rangeZones",
        "replay", "candidates", "future", "eventlog", "live", "trade",
        "ghostSwings", "ghostCandidates", "ghostPoints", "ghostTrends", "ghostMsbs", "ghostRanges",
        .. Enumerable.Range(0, 9).Select(x => $"level{x}"),
    ]);

    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string> { ["candles"] = "price", ["closes"] = "close" };
    private static readonly string[] Pins = ["open", "high", "low", "close"];
    private static readonly string[] Places = ["above", "below", "left", "right"];
    private static readonly string[] Keeps = ["section", "chapter", "always"];
    private static readonly int[] DefaultView = [0, 168];
    private const int EventSearchLimit = 3000;

    /// <summary>
    /// The datasets the local server serves, by id: a file under the repository root and, for a coarser dataset built from
    /// it, the hours its candles span. The first is the tour's own.
    /// </summary>
    public static readonly IReadOnlyList<(string Id, string Name, string Path, int Hours)> Datasets =
    [
        ("btc-1h", "BTC/USD hourly (Coinbase)", Path.Combine("CryptoAnalysis.Test", "TestData", "PricesExtensionsData", "COINBASE_BTCUSD, 60", "COINBASE_BTCUSD, 60.csv"), 1),
        ("btc-1h-2020", "BTC/USD hourly (Coinbase, from 2020)", Path.Combine("CryptoAnalysis.Test", "TestData", "COINBASE_BTCUSD, 60.csv"), 1),
        ("btc-4h-2020", "BTC/USD 4-hour (Coinbase, from 2020)", Path.Combine("CryptoAnalysis.Test", "TestData", "COINBASE_BTCUSD, 60.csv"), 4),
    ];

    /// <summary>
    /// Loads a dataset of the catalogue: its file, resampled to its candle length when that is more than an hour.
    /// </summary>
    public static List<Price> Load((string Id, string Name, string Path, int Hours) dataset, string repoRoot)
    {
        var prices = new Csv.CsvReaderHelper().ReadData<Price, global::CryptoAnalysis.Csv.ClassMaps.PriceClassMap>(Path.Combine(repoRoot, dataset.Path)).ToList();
        return dataset.Hours > 1 ? Resample.To(prices, TimeSpan.FromHours(dataset.Hours)) : prices;
    }

    /// <summary>
    /// The strategies of the Strategies page, as written, by id: those of strategies.json in the site's folder, or of
    /// strategies.example.json when there is none, as the page reads them. A section names one by its id.
    /// </summary>
    public static Dictionary<string, JsonNode> StrategiesOf(string siteDir)
    {
        var nodes = new Dictionary<string, JsonNode>();
        var path = new[] { "strategies.json", "strategies.example.json" }.Select(x => Path.Combine(siteDir, x)).FirstOrDefault(File.Exists);
        if (path == null)
            return nodes;
        foreach (var node in JsonNode.Parse(File.ReadAllText(path))?["strategies"]?.AsArray() ?? [])
        {
            if (node?["id"] is JsonValue id && id.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                nodes.TryAdd(text, node);
        }

        return nodes;
    }

    /// <summary>
    /// The pages a section can show in place of the chart, as they are, without being changed.
    /// </summary>
    public static readonly IReadOnlyList<string> Pages = ["Strategies"];

    /// <summary>
    /// What a text can do on the page its section shows, each an object with one of these keys: select (a strategy, by
    /// id), click (a button, by name), periods (the periods ticked, as "market:year"), set (a setting, by its path, with
    /// value), vary (a number's To and Step, by its path), count (a cost counted or not, with on) and cell (the period
    /// whose results are shown).
    /// </summary>
    public static readonly IReadOnlyList<string> PageActions = ["select", "click", "periods", "set", "vary", "count", "cell"];

    /// <summary>
    /// What is wrong with what a text does on a page and what it highlights there: "do" is a list of actions (see
    /// <see cref="PageActions"/>) and "highlight" names a part of the page; both need a section that shows a page, and a
    /// strategy selected is one of the Strategies page's.
    /// </summary>
    public static List<string> PageProblems(JsonObject cue, bool onPage, IReadOnlyDictionary<string, JsonNode> strategies)
    {
        var problems = new List<string>();
        if (cue["highlight"] is { } highlight && (highlight is not JsonValue part || !part.TryGetValue<string>(out _)))
            problems.Add("highlight names a part of the page shown, as \"entry\"");
        if (cue["do"] is { } does)
        {
            if (does is not JsonArray actions || actions.Any(x => x is not JsonObject { Count: > 0 }))
                return [.. problems, "do is a list of things done on the page shown, as [{ \"click\": \"run\" }]"];
            foreach (var action in actions.Select(x => x!.AsObject()))
            {
                if (PageActions.Count(action.ContainsKey) != 1)
                    problems.Add($"each thing done is one of {string.Join(", ", PageActions)}");
                else if (action["select"] is JsonValue id && id.TryGetValue<string>(out var selected) && !strategies.ContainsKey(selected))
                    problems.Add($"no strategy \"{selected}\" on the Strategies page");
            }
        }

        if (!onPage && (cue["highlight"] != null || cue["do"] != null))
            problems.Add("highlight and do are for a section that shows a page, with \"page\"");
        return problems;
    }

    /// <summary>
    /// Compiles the tour against one dataset, the tour's own: a tour without scenes needs no other.
    /// </summary>
    public static TourScript Compile(JsonNode def, IReadOnlyList<Price> dataset, bool teachOnce = false, IReadOnlyDictionary<string, JsonNode>? strategies = null)
    {
        return Compile(def, new Dictionary<string, IReadOnlyList<Price>> { [def["dataset"]?.GetValue<string>() ?? ""] = dataset }, teachOnce, strategies);
    }

    /// <summary>
    /// Compiles the tour against its datasets, by id: the candle each scene's start time lands on, then every section worked out
    /// from the start state and the sections before it. A scene names a dataset the tour does not have, or none, and it plays on
    /// the tour's own, which is the one named by the tour or else the first given. Problems are listed, not thrown. A written
    /// section never teaches a definition the tour has cited before it; with teachOnce, as tour.json is played start to end,
    /// nor one a written section before it has taught. An analysis, whose chapters are ticked on and off, teaches each in full.
    /// </summary>
    public static TourScript Compile(JsonNode def, IReadOnlyDictionary<string, IReadOnlyList<Price>> datasets, bool teachOnce = false, IReadOnlyDictionary<string, JsonNode>? strategies = null)
    {
        var errors = new List<string>();
        var known = new HashSet<string>(PageLayers.Concat(Terms.All.Select(x => x.Type.ToString())));
        var strategyNodes = strategies ?? new Dictionary<string, JsonNode>();
        var start = def["start"]?.AsObject();
        var own = def["dataset"]?.GetValue<string>() ?? "";
        var ownPrices = datasets.TryGetValue(own, out var found) ? found : datasets.Values.First();
        var anchor = AnchorOf(start?["time"]?.GetValue<string>(), ownPrices);
        var scenes = new List<TourScene> { new(own, anchor) };
        var prices = ownPrices.Skip(anchor).ToList();
        var candles = prices.Count;

        List<string> Ids(JsonNode? list, string where) => (list?.AsArray() ?? [])
            .Select(x => x?.GetValue<string>() ?? "")
            .Select(x => Aliases.TryGetValue(x, out var id) ? id : x)
            .Where(x => known.Contains(x) || !Add(errors, $"{where}: unknown layer \"{x}\""))
            .ToList();

        int[] Range(JsonNode? node, string where, int[] fallback)
        {
            var values = node?.AsArray().Select(x => x?.GetValue<int>()).ToArray();
            if (values is [int first, int last] && first >= 0 && last >= first)
                return [first, last];
            errors.Add($"{where}: the view is the first and last candle shown, the last at or after the first");
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
        var expanded = def.DeepClone();
        var expandedSections = expanded["sections"]?.AsArray();
        // The definitions told so far, which a written section does not teach again.
        var cited = new HashSet<string>();

        foreach (var (written, i) in (def["sections"]?.AsArray() ?? []).Select((x, i) => (x?.AsObject(), i)))
        {
            var where = $"section {i + 1}";
            var node = written;
            if (node == null)
            {
                errors.Add($"{where}: not a section");
                continue;
            }

            if (node["window"] != null)
                errors.Add($"{where}: \"window\" is now \"view\", the first and last candle shown");

            // A section that begins a scene starts a replay of its own: the candles count from #0 again and the view starts afresh.
            if (node["scene"] is JsonNode sceneNode)
            {
                var scene = sceneNode as JsonObject;
                var time = scene?["time"]?.GetValue<string>();
                var id = scene?["dataset"]?.GetValue<string>() ?? own;
                if (scene == null || time == null)
                    errors.Add($"{where}: a scene is {{ \"dataset\", \"time\" }}, on the tour's dataset unless one is given");
                if (!datasets.TryGetValue(id, out var sceneDataset))
                {
                    errors.Add($"{where}: the server has no dataset \"{id}\", so the scene plays on the tour's own");
                    sceneDataset = ownPrices;
                }

                var sceneAnchor = AnchorOf(time, sceneDataset);
                prices = sceneDataset.Skip(sceneAnchor).ToList();
                candles = prices.Count;
                if (candles < 2)
                    errors.Add($"{where}: the scene's time is past the end of its dataset");
                scenes.Add(new TourScene(id, sceneAnchor));
                at = 0;
                view = DefaultView;
                if (node["fastForward"] != null || node["rewind"] != null)
                    errors.Add($"{where}: a section that begins a scene has no journey: the scene starts a new replay");
            }

            // A section that explains something is written out from the prices before it is read like any other.
            if (node["explain"] != null)
            {
                // A Trade's strategy is one of the Strategies page's, named by its id.
                if (node["strategy"] is JsonValue named && named.TryGetValue<string>(out var strategyId))
                {
                    node = node.DeepClone().AsObject();
                    if (strategyNodes.TryGetValue(strategyId, out var strategyNode))
                        node["strategy"] = strategyNode.DeepClone();
                    else
                        errors.Add($"{where}: no strategy \"{strategyId}\" on the Strategies page");
                }

                // What the written texts taught, as written before any edit, is told.
                node = Explain.Expand(node, prices, node["from"] is JsonValue f && f.TryGetValue<int>(out var begins) ? begins : at, where, errors, cited, teachOnce ? cited : null);
                if (expandedSections != null)
                    expandedSections[i] = node.DeepClone();
            }

            // A section can show a page in place of the chart, as it is.
            var page = node["page"] is JsonValue shown && shown.TryGetValue<string>(out var pageName) ? pageName : null;
            if (node["page"] != null && (page == null || !Pages.Contains(page)))
                errors.Add($"{where}: page is a page shown in place of the chart: {string.Join(", ", Pages)}");

            if (node["layers"] != null)
                layers = Ids(node["layers"], where);
            var removed = Ids(node["remove"], where);
            layers = layers.Concat(Ids(node["add"], where)).Distinct().Where(x => !removed.Contains(x)).ToList();
            if (node["view"] != null)
                view = Range(node["view"], where, view);
            if (node["speed"] != null)
                speed = Rate(node["speed"], where, speed);

            // A journey takes the tour to where the section starts: a fast forward or a rewind that runs sets its first candle.
            var forward = node["fastForward"] != null;
            var tripName = forward ? "fast forward" : "rewind";
            var trip = node["fastForward"] ?? node["rewind"];
            int? journeyTo = null;
            if (node["fastForward"] != null && node["rewind"] != null)
                errors.Add($"{where}: a section fast forwards or rewinds, not both");
            if (trip is JsonObject journey)
            {
                if (journey["speed"] != null)
                    Rate(journey["speed"], where, 50);
                if (journey[forward ? "draw" : "erase"]?.GetValue<bool>() ?? true)
                {
                    var to = journey["to"] is JsonValue given && given.TryGetValue<int>(out var number) ? number : (int?)null;
                    if (to is int there && there >= 0 && there < candles && (forward ? there > at : there < at))
                        journeyTo = there;
                    else
                        errors.Add($"{where}: the {tripName} needs \"to\", a candle {(forward ? "after" : "before")} #{at}, where the section before ends");
                    if (node["from"] != null)
                        errors.Add($"{where}: the {tripName} takes the replay to where the section starts, so the section has no \"from\"");
                }
            }
            else if (trip != null)
            {
                errors.Add($"{where}: a {tripName} is {{ \"to\", \"speed\", \"{(forward ? "draw" : "erase")}\" }}");
            }

            var from = journeyTo ?? Candle(node["from"], prices, at, where, "from", errors) ?? at;
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
                if (cue != null)
                    errors.AddRange(PageProblems(cue, page != null, strategyNodes).Select(x => $"{what}: {x}"));
                var texts = new Dictionary<EnumDetail, string>();
                foreach (var (name, value) in cue?["texts"]?.AsObject() ?? [])
                {
                    if (Details.Parse(name) is { } level)
                        texts[level] = value?.GetValue<string>() ?? "";
                    else
                        errors.Add($"{what}: unknown detail \"{name}\": {string.Join(", ", Enum.GetValues<EnumDetail>().Select(Details.Name))}");
                }

                texts = texts.Where(x => x.Value.Length > 0).ToDictionary();
                // A definition, cited by its key and told in a direction when it has slots, is the text at Education. The
                // expanded tour has it written out, so what reads the texts aloud reads it too.
                if (cue?["define"]?.GetValue<string>() is { } define)
                {
                    var direction = cue["direction"]?.GetValue<string>() switch { "Up" => EnumSwingDirection.Up, "Down" => EnumSwingDirection.Down, _ => (EnumSwingDirection?)null };
                    if (!Definitions.Has(define))
                        errors.Add($"{what}: no definition \"{define}\" in definitions.json");
                    else if (direction == null && Definitions.NeedsDirection(define))
                        errors.Add($"{what}: the definition \"{define}\" is told in a direction: \"direction\": \"Up\" or \"Down\"");
                    else
                    {
                        texts[EnumDetail.Education] = Definitions.Text(define, direction);
                        cited.Add(texts[EnumDetail.Education]);
                        if (expandedSections?[i]?["cues"]?[j] is JsonObject told)
                            told["texts"] = Details.ToJson(texts);
                    }
                }

                if (cue?["text"] != null)
                    errors.Add($"{what}: \"text\" is now \"texts\", a text for each level of detail: {{ \"summary\", \"education\" }}");
                else if (texts.Count == 0)
                    errors.Add($"{what}: no text");
                var text = texts.Count == 0 ? "" : texts[texts.Keys.Max()];
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

                // A line: from the pinned point to another candle's point, reached when the replay reaches the line's end.
                (int, string)? lineTo = null;
                if (cue?["to"] is JsonNode toNode)
                {
                    var to = toNode as JsonObject;
                    var toAt = to == null ? null : Candle(to["at"], prices, from, what, "to", errors);
                    var toOn = to?["on"] is JsonValue toOnValue ? toOnValue.ToString() : on ?? "close";
                    if (to == null || to["at"] == null || on == null)
                        errors.Add($"{what}: a line needs a pinned candle and \"to\": {{ \"at\", \"on\" }}, the candle and point it runs to");
                    else if (toAt is int end && (end < 0 || end > at))
                        errors.Add($"{what}: the line's end, #{end}, is not reached, as the section ends at #{at}");
                    if (!Pins.Contains(toOn) && !double.TryParse(toOn, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                        errors.Add($"{what}: unknown \"on\" \"{toOn}\" for the line's end");
                    if (toAt is int lineEnd)
                        lineTo = (lineEnd, toOn);
                }

                if (cue?["heads"] is JsonNode headsNode && (headsNode is not JsonObject heads || heads.Any(x => x.Key != "start" && x.Key != "end")))
                    errors.Add($"{what}: heads is {{ \"start\", \"end\" }}, true or false each");
                cues.Add(new TourCue(j, texts, cueAt, on, place, keep, hold >= 0 ? hold.Value : Math.Max(2.5, text.Length / 14.0), lineTo));
            }

            var levels = layers.Where(x => x.StartsWith("level")).Select(x => int.Parse(x["level".Length..])).OrderBy(x => x).ToList();
            sections.Add(new TourSection(i, node["chapter"]?.GetValue<string>(), scenes.Count - 1, layers, levels.Count > 0 ? levels : [1], from, at, view, speed, cues, journeyTo != null ? tripName : null));
        }

        return new TourScript(def["title"]?.GetValue<string>(), def["dataset"]?.GetValue<string>(), anchor, scenes, sections, errors, expanded);
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
        return Facts(tour, new Dictionary<string, IReadOnlyList<Price>> { [tour.Dataset ?? ""] = dataset });
    }

    /// <summary>
    /// The facts of a tour with scenes, each scene's read from its own dataset; a line in a later scene says which.
    /// </summary>
    public static List<string> Facts(TourScript tour, IReadOnlyDictionary<string, IReadOnlyList<Price>> datasets)
    {
        var byScene = tour.Scenes.Select(scene =>
            (datasets.TryGetValue(scene.Dataset, out var found) ? found : datasets.Values.First()).Skip(scene.Anchor).ToList()).ToList();
        var lines = new List<string>();
        foreach (var s in tour.Sections)
        {
            var prices = byScene[s.Scene];
            var name = (s.Chapter != null ? $"section {s.Index + 1} ({s.Chapter})" : $"section {s.Index + 1}") + (s.Scene > 0 ? $" in scene {s.Scene + 1}" : "");
            if (s.Journey != null)
                lines.Add($"#{s.From} · {name} gets there by {s.Journey}: {At(prices, s.From, s.From, s.Levels)}");
            if (s.Until > s.From)
                lines.Add($"#{s.Until} · {name} runs to it: {At(prices, s.Until, s.Until, s.Levels)}");
            foreach (var c in s.Cues.Where(x => x.At != null))
            {
                lines.Add($"#{c.At} · {name} text {c.Index + 1} \"{c.Text}\"{(c.On != null ? $" on {c.On}" : "")} · seen at #{s.Until}: {At(prices, c.At!.Value, s.Until, s.Levels)}");
                if (c.LineTo is (int end, string endOn))
                    lines.Add($"#{end} · {name} text {c.Index + 1} line ends on {endOn} · seen at #{s.Until}: {At(prices, end, s.Until, s.Levels)}");
            }
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
            foreach (var range in Ranges.At(prefix, EnumPriceBasis.Close, level))
            {
                if (range.Identified.Time == time)
                    facts.Add($"L{level} range {range.Low.Price:F0}-{range.High.Price:F0} at {range.Retracement}%");
                if (range.End?.Time == time)
                    facts.Add($"L{level} range ends {(range.EndedAbove ? "above" : "below")}");
            }
        }

        return facts.Count > 0 ? string.Join(" · ", facts.Distinct()) : "nothing";
    }

    // A candle given as a number, or as an event to look for from a candle on.
    internal static int? Candle(JsonNode? node, List<Price> prices, int from, string where, string field, List<string> errors)
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
