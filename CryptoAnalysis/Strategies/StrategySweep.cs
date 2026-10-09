using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Gradient.CryptoAnalysis.Strategies;

/// <summary>
/// One variation of a sweep: the value of each number varied, and the strategy's baseline with those values. Error says why
/// it was not run instead, as when its entry condition is met nowhere.
/// </summary>
public sealed record SweepVariation(Dictionary<string, double> Values, BaselineRun? Baseline, string? Error);

/// <summary>
/// The best variation of a sweep by one total, set against the best of the random runs: for each seed, the best that total
/// came to over every variation's random run with that seed. Picking the best of many variations finds luck as well as
/// edge, and the random runs are picked from in the same way, so this is the fair test of the best. Variation is the index
/// of the best variation; null when none has a value for the total.
/// </summary>
public sealed record SweepBest(int? Variation, BaselineMeasure Against);

/// <summary>
/// A strategy swept over a dataset: each variation with its own random-entry baseline, and the best of them by each total.
/// </summary>
public sealed record SweepRun(Strategy Strategy, string Dataset, int Candles, int Runs, int Seed, List<string> Varied,
    List<SweepVariation> Variations, List<SweepBest> Best, DateTime Run);

/// <summary>
/// Runs a strategy once for each combination of the values of the numbers it varies (see <see cref="Strategy.Vary"/>), each
/// against its own random-entry baseline, as the exits a variation has change what random entries make. Every variation's
/// random runs use the same seeds, so a variation that changes only the exits is set against the same random candles.
/// </summary>
public static class StrategySweep
{
    public const int MaxVariations = 1000;

    /// <summary>
    /// Runs every variation of the strategy, each with the given number of random runs, and finds the best by each total.
    /// With a window, every variation enters only in it, as <see cref="StrategyBacktest.Run"/> does.
    /// </summary>
    public static SweepRun Run(IReadOnlyList<Price> prices, Strategy strategy, string dataset = "", int runs = StrategyBaseline.DefaultRuns, int seed = 1,
        (int First, int End)? window = null)
    {
        var variations = Variations(strategy);
        var measured = variations.Select(v =>
        {
            var errors = v.Strategy.Validate();
            if (errors.Count > 0)
                return (v.Values, Result: ((BaselineRun Baseline, BacktestSummary Actual, BacktestSummary[] Random)?)null, Error: string.Join(" ", errors));
            try
            {
                return (v.Values, Result: StrategyBaseline.Measured(prices, v.Strategy, dataset, runs, seed, window), Error: (string?)null);
            }
            catch (ArgumentException e)
            {
                return (v.Values, Result: null, Error: e.Message);
            }
        }).ToList();

        var best = StrategyBaseline.Totals.Select(t =>
        {
            int? top = null;
            double? value = null;
            for (var k = 0; k < measured.Count; k++)
            {
                if (measured[k].Result is { } r && t.Of(r.Actual) is double v && (value == null || v > value))
                    (top, value) = (k, v);
            }

            var luck = Enumerable.Range(0, runs)
                .Select(r => measured.Select(m => m.Result is { } x ? t.Of(x.Random[r]) : null).Max())
                .OfType<double>();
            return new SweepBest(top, StrategyBaseline.Measure(t.Name, value, luck));
        }).ToList();

        return new SweepRun(strategy, dataset, prices.Count, runs, seed, [.. (strategy.Vary ?? []).Keys],
            measured.Select(m => new SweepVariation(m.Values, m.Result?.Baseline, m.Error)).ToList(), best, DateTime.UtcNow);
    }

    /// <summary>
    /// The strategies a sweep runs, each with the values it was given: every combination of the values of the numbers the
    /// strategy varies, the last varied fastest. With nothing varied, the strategy alone. None of them varies anything itself.
    /// </summary>
    public static List<(Dictionary<string, double> Values, Strategy Strategy)> Variations(Strategy strategy)
    {
        var node = JsonSerializer.SerializeToNode(strategy, StrategyBook.JsonOptions)!.AsObject();
        node.Remove("vary");
        var ranges = (strategy.Vary ?? []).Select(x => (Path: x.Key, Values: Values(node, x.Key, x.Value))).ToList();
        var count = ranges.Aggregate(1L, (n, r) => n * r.Values.Count);
        if (count > MaxVariations)
            throw new ArgumentException($"The sweep has {count:N0} variations; at most {MaxVariations:N0} can be run at once.", nameof(strategy));

        IEnumerable<Dictionary<string, double>> combinations = [new Dictionary<string, double>()];
        foreach (var (path, values) in ranges)
            combinations = combinations.SelectMany(c => values.Select(v => new Dictionary<string, double>(c) { [path] = v }));
        return combinations.Select(c => (c, Apply(node, c))).ToList();
    }

    /// <summary>
    /// The values a number of the strategy, at a path in its JSON, takes in a sweep: from its own value up to the range's To
    /// in its Steps, To included when a step lands on it.
    /// </summary>
    public static List<double> Values(JsonObject strategy, string path, StrategyRange range)
    {
        if (Find(strategy, path) is not JsonValue value || value.GetValueKind() != JsonValueKind.Number)
            throw new ArgumentException($"\"{path}\" is not a number of the strategy, so it cannot be varied.");
        var from = double.Parse(value.ToJsonString(), CultureInfo.InvariantCulture);
        if (range.Step <= 0)
            throw new ArgumentException($"{path}: the step must be more than 0.");
        if (range.To < from)
            throw new ArgumentException($"{path}: To must be at least the value it varies from, {from}.");
        var count = (long)Math.Floor((range.To - from) / range.Step + 1e-9) + 1;
        if (count > MaxVariations)
            throw new ArgumentException($"{path}: from {from} to {range.To} in steps of {range.Step} is {count:N0} values; at most {MaxVariations:N0} can be run at once.");

        var values = Enumerable.Range(0, (int)count).Select(k => Math.Round(from + k * range.Step, 10)).ToList();
        // A whole number, as a Length or a count of candles, cannot take a step that is not whole.
        if (values.Count > 1)
        {
            try
            {
                Apply(strategy, new Dictionary<string, double> { [path] = values[1] });
            }
            catch (ArgumentException)
            {
                throw new ArgumentException($"{path} is a whole number, so its step must be whole.");
            }
        }

        return values;
    }

    /// <summary>
    /// The node at a path in a strategy's JSON, as "stopLoss.ratio" or "after.0.length"; null when there is none.
    /// </summary>
    public static JsonNode? Find(JsonNode? node, string path)
    {
        foreach (var part in path.Split('.'))
        {
            node = node switch
            {
                JsonObject o => o[part],
                JsonArray a when int.TryParse(part, out var i) && i >= 0 && i < a.Count => a[i],
                _ => null,
            };
        }

        return node;
    }

    private static Strategy Apply(JsonObject node, Dictionary<string, double> values)
    {
        var copy = node.DeepClone().AsObject();
        foreach (var (path, value) in values)
        {
            var dot = path.LastIndexOf('.');
            var parent = dot < 0 ? copy : Find(copy, path[..dot]);
            var key = path[(dot + 1)..];
            if (parent is JsonObject o)
                o[key] = value;
            else if (parent is JsonArray a && int.TryParse(key, out var i))
                a[i] = value;
        }

        try
        {
            return copy.Deserialize<Strategy>(StrategyBook.JsonOptions)!;
        }
        catch (JsonException e)
        {
            throw new ArgumentException(e.Message, e);
        }
    }
}
