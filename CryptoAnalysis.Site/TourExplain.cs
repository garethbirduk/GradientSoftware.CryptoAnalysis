using System.Globalization;
using System.Text.Json.Nodes;

namespace Gradient.CryptoAnalysis.Site;

/// <summary>
/// Writes tour sections from the prices. A section of tour.json that says { "explain": "Candle", "at": 48 } is filled in
/// here: its view, how far the replay runs so the thing explained is drawn, and its texts, worked out from the candle it
/// names, so the same section explains whichever candle the tour points it at. What the section says for itself wins: a
/// view or until it gives is kept, and texts of its own follow the written ones.
/// </summary>
public static class Explain
{
    /// <summary>
    /// The things a section can explain.
    /// </summary>
    public static readonly IReadOnlyList<string> Terms = ["Candle"];
    private static readonly string[] Numbers = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve"];
    private const int CandlesBefore = 4;
    private const int CandlesAfter = 3;

    /// <summary>
    /// The section with its written parts filled in, from the prices of its scene and the candle the replay has reached
    /// before it (so the section runs on to the candle it explains when that is further on). The section's "explain" and
    /// "at" are taken out, so what comes back compiles as any other section does. Problems are listed, not thrown, and a
    /// section with a problem comes back with only what it says for itself.
    /// </summary>
    public static JsonObject Expand(JsonObject node, List<Price> prices, int reached, string where, List<string> errors)
    {
        var result = (JsonObject)node.DeepClone();
        result.Remove("explain");
        result.Remove("at");
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

        var written = Candle(prices, at.Value, reached);
        foreach (var (key, value) in written.Where(x => x.Key != "cues"))
            result[key] ??= value?.DeepClone();
        var cues = new JsonArray();
        foreach (var cue in (written["cues"]?.AsArray() ?? []).Concat(node["cues"]?.AsArray() ?? []))
            cues.Add(cue?.DeepClone());
        result["cues"] = cues;
        return result;
    }

    /// <summary>
    /// A tour of its own around one thing, in the shape of tour.json: a start a few candles before the candle a time lands
    /// on, with candles drawn, a silent section that runs to the candle, and the section that explains it, which so begins
    /// with the candle drawn and says its texts in their own order. Null when the term is not one that can be explained.
    /// </summary>
    public static JsonObject? Tour(string term, string dataset, IReadOnlyList<Price> prices, string time)
    {
        if (!Terms.Contains(term) || prices.Count == 0)
            return null;

        var index = Tours.AnchorOf(time, prices);
        var anchor = Math.Max(0, index - CandlesBefore * 2);
        var at = index - anchor;
        return new JsonObject
        {
            ["title"] = $"{term} at {prices[index].DateTime:HH:mm} on {prices[index].DateTime.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}",
            ["dataset"] = dataset,
            ["start"] = new JsonObject
            {
                ["time"] = prices[anchor].DateTime.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
                ["view"] = new JsonArray(0, at + CandlesAfter),
                ["speed"] = 4.0,
                ["layers"] = new JsonArray("level1", "candles"),
            },
            ["sections"] = new JsonArray(
                new JsonObject { ["chapter"] = term, ["until"] = at, ["speed"] = 8.0 },
                new JsonObject { ["explain"] = term, ["at"] = at }),
        };
    }

    /// <summary>
    /// The written parts of a section that explains one candle: a view with it a little right of centre, until when the
    /// replay has not reached it, and the texts: when it is, its four prices pinned to it, why it is green or red, and the
    /// nearest candle of the other colour for contrast.
    /// </summary>
    public static JsonObject Candle(List<Price> prices, int at, int reached)
    {
        var p = prices[at];
        var length = prices.Count > 1 ? prices[1].DateTime - prices[0].DateTime : TimeSpan.FromHours(1);
        var up = p.Close > p.Open;
        var flat = p.Close == p.Open;
        var drawn = Math.Max(at, reached);
        var view = new[] { Math.Max(0, at - CandlesBefore), at + CandlesAfter };

        // Every text waits for the candle, so the replay draws it before anything is said, whether or not it was there already.
        var cues = new JsonArray
        {
            Text(at, $"This candle is {When(p.DateTime, length)}."),
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

        var section = new JsonObject { ["view"] = new JsonArray(view[0], view[1]) };
        if (at > reached)
            section["until"] = at;
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

    // A price as the tour writes them: rounded down, no decimals.
    private static string Money(double price) => Math.Floor(price).ToString("0", CultureInfo.InvariantCulture);

    // "the hour from 00:00 to 01:00 on Tuesday 3 January 2023", or the day for daily candles.
    private static string When(DateTime start, TimeSpan length)
    {
        var day = start.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture);
        if (length >= TimeSpan.FromDays(1))
            return $"the day of {day}";
        var hours = (int)Math.Round(length.TotalHours);
        var span = hours == 1 ? "hour" : $"{(hours < Numbers.Length ? Numbers[hours] : hours.ToString(CultureInfo.InvariantCulture))} hours";
        return $"the {span} from {start:HH:mm} to {start + length:HH:mm} on {day}";
    }

    // Another candle by where it is from the one explained.
    private static string Which(int offset) => offset switch
    {
        -1 => "The candle before it",
        1 => "The candle after it",
        < 0 => $"{Capital(Numbers[-offset])} candles earlier, the candle",
        _ => $"{Capital(Numbers[offset])} candles later, the candle",
    };

    private static string Capital(string word) => char.ToUpperInvariant(word[0]) + word[1..];
}
