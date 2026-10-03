using System.Globalization;
using System.Text.Json.Nodes;

namespace Gradient.CryptoAnalysis.Site;

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
    public static readonly IReadOnlyList<string> Terms = ["Candle", "Swing", "Trend"];

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
    /// The section with its written parts filled in, from the prices of its scene and the candle the replay has reached
    /// before it (so the section runs on to the thing it explains when that is further on). The section's "explain", "at"
    /// and "level" are taken out, so what comes back compiles as any other section does. Problems are listed, not thrown,
    /// and a section with a problem comes back with only what it says for itself.
    /// </summary>
    public static JsonObject Expand(JsonObject node, List<Price> prices, int reached, string where, List<string> errors)
    {
        var result = (JsonObject)node.DeepClone();
        result.Remove("explain");
        result.Remove("at");
        result.Remove("level");
        var term = node["explain"]?.GetValue<string>() ?? "";
        if (!Terms.Contains(term))
        {
            errors.Add($"{where}: explain names \"{term}\", which is not a thing the tour can explain: {string.Join(", ", Terms)}");
            return result;
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
        var written = Write(term, prices, at.Value, level, reached, until, out var problem);
        if (written == null)
        {
            errors.Add($"{where}: {problem}");
            return result;
        }

        foreach (var (key, value) in written.Where(x => x.Key != "cues"))
            result[key] ??= value?.DeepClone();
        var cues = new JsonArray();
        foreach (var cue in (written["cues"]?.AsArray() ?? []).Concat(node["cues"]?.AsArray() ?? []))
            cues.Add(cue?.DeepClone());
        result["cues"] = cues;
        return result;
    }

    /// <summary>
    /// A tour of its own around one thing, in the shape of tour.json. For a Candle it starts a few candles before the one
    /// a time lands on, with candles drawn, runs to it in silence and then explains it, so the texts come in their own
    /// order. For a Swing or a Trend it starts where the dataset does, so the structure is the Replay page's, and the
    /// section jumps to just before the thing and runs through it, the texts coming as the replay reaches each part: a
    /// Swing to its BoS, a Trend to the candle. The thing is looked for in the prices up to seen, the Replay page's cursor,
    /// so it is the one the page showed. Null when the term is not one that can be explained or nothing of the kind is at
    /// that candle.
    /// </summary>
    public static JsonObject? Tour(string term, string dataset, List<Price> prices, string time, int level = 1, string? seen = null)
    {
        if (!Terms.Contains(term) || prices.Count == 0)
            return null;

        var index = Tours.AnchorOf(time, prices);
        var when = prices[index].DateTime;
        var title = $"{term} at {when:HH:mm} on {when.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}";
        if (term == "Candle")
        {
            var anchor = Math.Max(0, index - CandlesBefore * 2);
            var at = index - anchor;
            return TourOf(title, dataset, prices[anchor].DateTime, [0, at + CandlesAfter], ["level1", "candles"],
                new JsonObject { ["chapter"] = term, ["until"] = at, ["speed"] = 8.0 },
                new JsonObject { ["explain"] = term, ["at"] = at });
        }

        var cursor = seen == null ? prices.Count - 1 : Tours.AnchorOf(seen, prices);
        if (index > cursor)
            return null;
        // The thing as the page showed it at its cursor. The section runs to the thing's own end when the chart has it
        // there too (a Swing's BoS, a Trend's second BoS or the candle), else on to the cursor, as the finer levels can be
        // anchored afresh by the candles between.
        var known = cursor < prices.Count - 1 ? prices.GetRange(0, cursor + 1) : prices;
        int first, until;
        if (term == "Swing")
        {
            if (SwingAt(known, index, level) is not { } swing)
                return null;
            first = swing.Span.First;
            var bos = swing.Span.Last;
            until = SwingAt(prices.GetRange(0, bos + 1), first, level, swing.Swing.Direction) != null ? bos : cursor;
        }
        else
        {
            if (TrendAt(known, index, level) is not { } trend)
                return null;
            first = trend.Start;
            var made = Math.Max(index, trend.Index[trend.Trend.Confirmed.Time]);
            until = TrendAt(prices.GetRange(0, made + 1), first, level, trend.Trend.Direction) != null ? made : cursor;
        }

        var from = Math.Max(0, first - Margin);
        title = $"{Order(level)} {title}";
        return TourOf(title, dataset, prices[0].DateTime, [from, until + Margin], [$"level{level}", "close"],
            new JsonObject { ["chapter"] = term, ["explain"] = term, ["at"] = index, ["level"] = level, ["from"] = from, ["until"] = until, ["speed"] = Pace(until - from) });
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

    // Candles a second for a section that runs a stretch: about twenty seconds of replay, between a walk and a sprint.
    private static double Pace(int candles) => Math.Clamp(Math.Round(candles / 20.0), 4, 200);

    private static JsonObject? Write(string term, List<Price> prices, int at, int level, int reached, int? until, out string problem)
    {
        problem = "";
        switch (term)
        {
            case "Candle":
                return Candle(prices, at, reached);
            case "Swing":
                return Swing(prices, at, level, reached, out problem, until);
            default:
                return Trend(prices, at, level, reached, out problem, until);
        }
    }

    /// <summary>
    /// The written parts of a section that explains one candle: a view with it a little right of centre, until when the
    /// replay has not reached it, and the texts: when it is, its four prices pinned to it, why it is green or red, and the
    /// nearest candle of the other colour for contrast.
    /// </summary>
    public static JsonObject Candle(List<Price> prices, int at, int reached)
    {
        var p = prices[at];
        var length = Length(prices);
        var up = p.Close > p.Open;
        var flat = p.Close == p.Open;
        var drawn = Math.Max(at, reached);
        var view = new[] { Math.Max(0, at - CandlesBefore), at + CandlesAfter };

        // Every text waits for the candle, so the replay draws it before anything is said, whether or not it was there already.
        var cues = new JsonArray
        {
            Text(at, $"This candle is {Span(p.DateTime, length)}."),
            Text(at, $"It opened at {Money(p.Open)} and closed at {Money(p.Close)}.", hold: 0),
            Pin(at, "open", "left", $"open: {Money(p.Open)}", 4),
            Pin(at, "close", "right", $"close: {Money(p.Close)}", 4),
            Text(at, $"Its high was {Money(p.High)} and its low was {Money(p.Low)}.", hold: 0),
            Pin(at, "high", "above", $"high: {Money(p.High)}", 2.5),
            Pin(at, "low", "below", $"low: {Money(p.Low)}", 2.5),
            Text(at, flat
                ? "The candle closed where it opened, so it has no body: it is neither green nor red."
                : $"Because the candle closed {(up ? "higher" : "lower")} than the open, it is {(up ? "green" : "red")}."),
        };

        // The nearest drawn candle in view of the other colour, the one before preferred, to set against it.
        if (!flat)
        {
            var other = Enumerable.Range(1, CandlesBefore + CandlesAfter)
                .SelectMany(d => new[] { at - d, at + d })
                .Where(i => i >= view[0] && i <= Math.Min(drawn, view[1]) && i != at)
                .FirstOrDefault(i => up ? prices[i].Close < prices[i].Open : prices[i].Close > prices[i].Open, -1);
            if (other >= 0)
            {
                var colour = up ? "red" : "green";
                cues.Add(Text(at, $"{Which(other - at)} closed {(up ? "lower" : "higher")} than the open, so it is {colour}.", hold: 0));
                cues.Add(Pin(other, up ? "low" : "high", up ? "below" : "above", colour, 3.5));
            }
        }

        return Section(view, at > reached ? at : null, ["candles"], cues);
    }

    /// <summary>
    /// The written parts of a section that explains the Swing at a level that a candle is in, as the chart has it where the
    /// section ends: at until when the section says so, else once the Swing's BoS is drawn. Where it begins and at what
    /// kind of high or low, the fall or rise to its Swing Low or High, an MSB on the way when there is one, the BoS that
    /// Confirms it, its two legs, and whether it is Strong or Weak. Null, with the problem, when no Swing at that level has
    /// the candle.
    /// </summary>
    public static JsonObject? Swing(List<Price> prices, int at, int level, int reached, out string problem, int? runsTo = null)
    {
        FoundSwing? seen;
        int end;
        if (runsTo is int given)
        {
            end = Math.Max(reached, given);
            seen = SwingAt(prices.GetRange(0, end + 1), at, level);
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

        var (swing, span, breaks, points, index) = (seen.Swing, seen.Span, seen.Breaks, seen.Points, seen.Index);
        var up = swing.Direction == EnumSwingDirection.Up;
        var length = Length(prices);
        var name = up ? "Upswing" : "Downswing";
        var startKind = up ? "high" : "low";
        var turn = up ? "Swing Low" : "Swing High";
        var extremeAt = index[swing.Extreme.Time];
        var bosAt = span.Last;
        var startLabel = LabelAt(points, swing.Start.Time);
        var turnLabel = LabelAt(points, swing.Extreme.Time);
        var against = up ? EnumAnnotationType.BearishMarketStructureBreak : EnumAnnotationType.BullishMarketStructureBreak;
        var msb = breaks.FirstOrDefault(b => b.Type == against && b.Break.Time > swing.Start.Time && b.Break.Time < swing.BreakOfStructure!.Time);
        var startPlace = up ? "above" : "below";
        var turnPlace = up ? "below" : "above";

        var cues = new JsonArray
        {
            Text(span.First, $"This is a {Order(level)} {name}. It begins {At(swing.Start.Time, length)}, at {Named(startLabel, startKind)}."),
            Pin(span.First, "close", Edge(span.First, startPlace), $"{startLabel ?? startKind}: {Money(swing.Start.Price)}", 4),
            Text(extremeAt, $"The price {(up ? "falls" : "rises")} for {Duration(extremeAt - span.First, length)}, to the {turn}: {Named(turnLabel, up ? "low" : "high")}."),
            Pin(extremeAt, "close", turnPlace, $"{turn}: {Money(swing.Extreme.Price)}", 4),
        };
        if (msb != null)
        {
            cues.Add(Text(index[msb.Break.Time], $"On the way, the price closes {(up ? "below" : "above")} the {turn} of the {name} before: an MSB."));
            cues.Add(Pin(index[msb.Break.Time], "close", "left", "MSB", 3.5));
        }
        cues.Add(Text(bosAt, $"{Capital(Duration(bosAt - extremeAt, length))} after the {turn}, the price closes {(up ? "above" : "below")} the {startKind} it began at: the BoS that Confirms the {name}."));
        cues.Add(Pin(bosAt, "close", startPlace, "BoS", 4));
        cues.Add(Text(bosAt, up
            ? $"Its Downleg runs from the high to the Swing Low, and its Upleg from the Swing Low to the BoS. The Upswing lasted {Duration(bosAt - span.First, length)} in all."
            : $"Its Upleg runs from the low to the Swing High, and its Downleg from the Swing High to the BoS. The Downswing lasted {Duration(bosAt - span.First, length)} in all."));
        cues.Add(Text(bosAt, msb == null
            ? $"No MSB fell inside it, so it is a Strong {name}."
            : $"The MSB inside it makes it a Weak {name}."));

        var layers = new List<string> { $"level{level}", name, up ? "BullishBreakOfStructure" : "BearishBreakOfStructure", up ? "bosLevelUp" : "bosLevelDown" };
        layers.AddRange(new[] { startLabel, turnLabel }.Where(x => x != null).Select(x => PointLayer(x!)));
        if (msb != null)
            layers.AddRange([up ? "BearishMarketStructureBreak" : "BullishMarketStructureBreak", up ? "msbLevelDown" : "msbLevelUp"]);
        problem = "";
        return Section([Math.Max(0, span.First - Margin), Math.Max(bosAt, end) + Margin], end > reached ? end : null, layers, cues, byCandle: true);
    }

    /// <summary>
    /// The written parts of a section that explains the Trend at a level that a candle is in, as the chart has it where
    /// the section ends: at until when the section says so, else at the candle, or where the second BoS makes it a Trend.
    /// Where it begins, that second BoS, its count, which of its Swings are Weak, its Strength, and whether it has ended at
    /// a Swing the other way or still runs. Null, with the problem, when no Trend at that level has the candle.
    /// </summary>
    public static JsonObject? Trend(List<Price> prices, int at, int level, int reached, out string problem, int? runsTo = null)
    {
        FoundTrend? seen;
        int end;
        if (runsTo is int given)
        {
            end = Math.Max(reached, given);
            seen = TrendAt(prices.GetRange(0, end + 1), at, level);
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

        var until = end;
        var (trend, index) = (seen.Trend, seen.Index);
        var up = trend.Direction == EnumSwingDirection.Up;
        var length = Length(prices);
        var name = up ? "Uptrend" : "Downtrend";
        var swing = up ? "Upswing" : "Downswing";
        var other = up ? "Downswing" : "Upswing";
        var turn = up ? "Swing Low" : "Swing High";
        var place = up ? "above" : "below";
        var startAt = seen.Start;
        var confirmedAt = index[trend.Confirmed.Time];
        var count = trend.Swings;
        var weak = trend.Parts.Select((part, i) => (part, i)).Where(x => !x.part.Strong).Select(x => Ordinal(x.i + 1)).ToList();
        var ended = trend.End != null && index.TryGetValue(trend.End.Time, out var endAt) && endAt <= until;

        var cues = new JsonArray
        {
            Text(startAt, $"This is a {Order(level)} {name}. It begins where its first {swing} begins, {At(trend.Start.Time, length)}."),
            Pin(startAt, "close", Edge(startAt, place), $"{name} begins", 4),
            Text(confirmedAt, $"From the BoS of its second {swing} there are two in a row: {An(name)}."),
            Pin(confirmedAt, "close", place, $"second {swing}", 4),
            Text(until, $"{(ended ? "By its end" : "Here")} it is {An(name)} of {Words(count)} {swing}s."),
            Text(until, weak.Count == 0
                ? $"Every one of them is Strong: no MSB fell inside any of them."
                : $"{Capital(Words(weak.Count))} of the {Words(count)} {(weak.Count == 1 ? "is" : "are")} Weak: the {List(weak)}. {(weak.Count == 1 ? "It" : "Each")} has an MSB inside it, a close {(up ? "below" : "above")} the {turn} of the {swing} before."),
            Text(until, $"Its Strength is {Words(trend.Strong)} of {Words(count)}: {trend.Strength}%."),
        };
        if (ended)
        {
            cues.Add(Text(until, $"It ended {At(trend.End!.Time, length)}, at the BoS of the first {other} since it began."));
            cues.Add(Pin(index[trend.End.Time], "close", up ? "below" : "above", $"{name} ends", 4));
        }
        else
        {
            cues.Add(Text(until, $"No {other} has been Confirmed since, so it is still running."));
        }

        var layers = new List<string> { $"level{level}", swing, name, "trendArrows", up ? "BullishBreakOfStructure" : "BearishBreakOfStructure" };
        if (weak.Count > 0)
            layers.Add(up ? "BearishMarketStructureBreak" : "BullishMarketStructureBreak");
        if (ended)
            layers.Add(other);
        problem = "";
        return Section([Math.Max(0, startAt - Margin), until + Margin], until > reached ? until : null, layers, cues, byCandle: true);
    }

    // What the structure at a level has, read from these prices alone.
    private sealed record Structure(List<SwingOutline> Swings, List<MarketStructureBreakOutline> Breaks, List<TrendOutline> Trends, List<StructurePoint> Points, Dictionary<DateTime, int> Index);

    private sealed record FoundSwing(SwingOutline Swing, (int First, int Last) Span, List<MarketStructureBreakOutline> Breaks, List<StructurePoint> Points, Dictionary<DateTime, int> Index);

    private sealed record FoundTrend(TrendOutline Trend, int Start, (int First, int Last) Span, Dictionary<DateTime, int> Index);

    private static Structure? Read(List<Price> prices, int level)
    {
        var levels = Sawtooth.Levels(prices, Basis, level + 1);
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
        var time = prices[at].DateTime;
        var swing = direction != null
            ? s.Swings.FirstOrDefault(x => x.Start.Time == time && x.Direction == direction && x.BreakOfStructure != null)
            : s.Swings.Where(x => x.BreakOfStructure != null).LastOrDefault(x => x.Start.Time <= time && x.BreakOfStructure!.Time >= time);
        return swing == null ? null : new FoundSwing(swing, (s.Index[swing.Start.Time], s.Index[swing.BreakOfStructure!.Time]), s.Breaks, s.Points, s.Index);
    }

    // The Trend at a level that has the candle: begun at or before it and not ended before it. With a direction, only a
    // Trend beginning at the candle that runs that way.
    private static FoundTrend? TrendAt(List<Price> prices, int at, int level, EnumSwingDirection? direction = null)
    {
        if (Read(prices, level) is not { } s)
            return null;
        var time = prices[at].DateTime;
        var trend = direction != null
            ? s.Trends.FirstOrDefault(x => x.Start.Time == time && x.Direction == direction)
            : s.Trends.LastOrDefault(x => x.Start.Time <= time && (x.End == null || x.End.Time >= time));
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

    private static JsonObject Text(int at, string text, double? hold = null)
    {
        var cue = new JsonObject { ["at"] = at, ["text"] = text };
        if (hold != null)
            cue["hold"] = hold;
        return cue;
    }

    private static JsonObject Pin(int at, string on, string place, string text, double hold) =>
        new() { ["at"] = at, ["on"] = on, ["place"] = place, ["text"] = text, ["hold"] = hold, ["voice"] = false };

    // A pin on a candle at the chart's left edge goes to the right of it, where there is room for it.
    private static string Edge(int at, string place) => at < Margin ? "right" : place;

    private static TimeSpan Length(IReadOnlyList<Price> prices) => prices.Count > 1 ? prices[1].DateTime - prices[0].DateTime : TimeSpan.FromHours(1);

    private static string? LabelAt(List<StructurePoint> points, DateTime time) =>
        points.FirstOrDefault(x => x.Time == time && x.Type != null && x.Label.Length > 0)?.Label;

    // The page's layer for a point's label.
    private static string PointLayer(string label) => label switch { "HH" => "HigherHigh", "HL" => "HigherLow", "LL" => "LowerLow", _ => "LowerHigh" };

    // "an HH", or the plain word when the point has no label.
    private static string Named(string? label, string plain) => label != null ? $"an {label}" : $"a {plain}";

    private static string An(string word) => $"{("aeiou".Contains(char.ToLowerInvariant(word[0])) ? "an" : "a")} {word}";

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
