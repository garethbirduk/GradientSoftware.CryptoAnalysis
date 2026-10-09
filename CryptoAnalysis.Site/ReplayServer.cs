using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gradient.CryptoAnalysis.Strategies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace Gradient.CryptoAnalysis.Site;

/// <summary>
/// A price series the server can replay, anchored at its first candle.
/// </summary>
public sealed record Dataset(string Id, string Name, List<Price> Prices);

/// <summary>
/// Serves the built site and, for replays too long to build into it, the replay in batches: the structure as known at each
/// candle of a range, computed from the dataset's first candle, or from a later candle given as the anchor. Local only.
/// </summary>
public static class ReplayServer
{
    /// <summary>
    /// Candles per batch, and how many candles before a batch the widest replay window shows (a month of hourly candles), so
    /// items older than that are left out.
    /// </summary>
    public const int BatchSize = 50;
    public const int Window = 744;
    private const int MaxLevel = 8;

    private static readonly JsonSerializerOptions Json = new(TermAnnotations.JsonOptions) { WriteIndented = false };

    /// <summary>
    /// Runs the server on localhost until stopped. Files in the source folder, when given, are served as they are now rather
    /// than as they were built, so the page and the tour can be edited without rebuilding.
    /// </summary>
    public static async Task Run(string siteDir, IReadOnlyList<Dataset> datasets, int port, string? sourceDir = null, TourVideo? video = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");
        builder.Services.AddResponseCompression(o => o.Providers.Add<GzipCompressionProvider>());
        var app = builder.Build();
        app.UseResponseCompression();

        IFileProvider files = Directory.Exists(sourceDir)
            ? new CompositeFileProvider(new PhysicalFileProvider(sourceDir), new PhysicalFileProvider(siteDir))
            : new PhysicalFileProvider(siteDir);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        // The site is rebuilt in place, so the browser must check for a newer page and data each time.
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
        });

        var byId = datasets.ToDictionary(x => x.Id);
        var batches = new ConcurrentDictionary<(string Dataset, int Anchor, int Start, (double Retracement, double Band) Settings), Lazy<string>>();

        // The static page, opened as a file, asks whether the server is running so it can link to it.
        app.MapGet("/api/health", (HttpContext context) =>
        {
            context.Response.Headers.AccessControlAllowOrigin = "*";
            return Results.Ok(new { ok = true });
        });

        app.MapGet("/api/datasets", () => Results.Json(
            datasets.Select(x => new { x.Id, x.Name, Candles = x.Prices.Count, First = x.Prices[0].DateTime, Last = x.Prices[^1].DateTime }), Json));

        app.MapGet("/api/prices", (string dataset) => byId.TryGetValue(dataset, out var data)
            ? Results.Json(new
            {
                T = data.Prices.Select(p => p.DateTime),
                O = data.Prices.Select(p => p.Open),
                H = data.Prices.Select(p => p.High),
                L = data.Prices.Select(p => p.Low),
                C = data.Prices.Select(p => p.Close),
            }, Json)
            : Results.NotFound());

        // With an anchor, the replay is of the dataset from that candle on: from counts from it, and so does the batch. The
        // range settings (the least retracement and the band, as percentages) are the page's, so each pair has batches of its own.
        app.MapGet("/api/replay", (string dataset, int from, int? anchor, double? retracement, double? band) =>
        {
            if (!byId.TryGetValue(dataset, out var data))
                return Results.NotFound();

            var first = Math.Clamp(anchor ?? 0, 0, data.Prices.Count - 1);
            var start = Math.Clamp(from / BatchSize * BatchSize, 0, data.Prices.Count - first - 1);
            var settings = (Retracement: retracement ?? Ranges.DefaultMinRetracement, Band: band ?? Ranges.DefaultBand);
            var json = batches.GetOrAdd((dataset, first, start, settings), key => new Lazy<string>(() => Batch(data, key.Anchor, key.Start, key.Settings))).Value;
            return Results.Text(json, "application/json");
        });

        // The whole dataset as known at its last candle, everything in it kept: what the Chart page shows.
        // Kept as UTF-8, as a long dataset's comes to tens of megabytes.
        var completes = new ConcurrentDictionary<(string Dataset, (double Retracement, double Band) Settings), Lazy<byte[]>>();
        app.MapGet("/api/complete", (string dataset, double? retracement, double? band) =>
        {
            if (!byId.TryGetValue(dataset, out var data))
                return Results.NotFound();

            var settings = (Retracement: retracement ?? Ranges.DefaultMinRetracement, Band: band ?? Ranges.DefaultBand);
            var json = completes.GetOrAdd((dataset, settings), key => new Lazy<byte[]>(() => Complete(data, key.Settings))).Value;
            return Results.Bytes(json, "application/json");
        });

        // A tour can wait for an event rather than a candle number: the nth time something becomes known at a level, looking
        // from a candle on, with only the prices up to each candle.
        var events = new ConcurrentDictionary<(string, int, int, EnumAnnotationType, int, int), Lazy<int?>>();
        app.MapGet("/api/event", (string dataset, int from, string type, int? anchor, int? level, int? nth) =>
        {
            if (!byId.TryGetValue(dataset, out var data) || !Enum.TryParse<EnumAnnotationType>(type, out var kind))
                return Results.NotFound();

            var first = Math.Clamp(anchor ?? 0, 0, data.Prices.Count - 1);
            var key = (dataset, first, from, kind, level ?? 1, nth ?? 1);
            var index = events.GetOrAdd(key, k => new Lazy<int?>(() =>
                Tours.FindEvent(data.Prices.GetRange(k.Item2, data.Prices.Count - k.Item2), k.Item3, k.Item4, k.Item5, k.Item6))).Value;
            return Results.Json(new { Index = index }, Json);
        });

        // A section that explains something is written out here from the prices of its scene (see Explain): the page sends the
        // section as the tour has it, with the candle the replay has reached before it, and gets back the section to play.
        app.MapPost("/api/explain", async (HttpRequest request) =>
        {
            JsonObject? body;
            try
            {
                body = await JsonNode.ParseAsync(request.Body) as JsonObject;
            }
            catch (JsonException)
            {
                return Results.BadRequest("The request is not valid JSON.");
            }

            if (body?["section"] is not JsonObject section || !byId.TryGetValue(body["dataset"]?.GetValue<string>() ?? "", out var data))
                return Results.BadRequest("The request is { dataset, anchor, from, where, cited, section }.");

            var first = Math.Clamp(body["anchor"]?.GetValue<int>() ?? 0, 0, data.Prices.Count - 1);
            var errors = new List<string>();
            // The definitions the tour has cited before the section, which it does not teach again.
            var cited = (body["cited"] as JsonArray ?? []).Select(x => x?.GetValue<string>() ?? "").ToHashSet();
            var expanded = Explain.Expand(section, data.Prices.GetRange(first, data.Prices.Count - first), body["from"]?.GetValue<int>() ?? 0,
                body["where"]?.GetValue<string>() ?? "the section", errors, cited);
            return Results.Json(new { Section = expanded, Errors = errors }, Json);
        });

        // A tour of its own around one thing, for the Tour page opened with ?explain=Swing&dataset=btc-1h&at=2023-01-03T00:00&level=1.
        // seen is the Replay page's cursor, so the Swing or Trend is the one the page showed there. The written tour takes
        // the glossary of the tour in tour.json, so the voice says BoS and MSB as that does, and its texts are read aloud in
        // the background at once, so the clips are there by the time Voice is pressed.
        var byDataset = datasets.ToDictionary(x => x.Id, x => (IReadOnlyList<Price>)x.Prices);
        var tourFile = sourceDir != null ? Path.Combine(sourceDir, "tour.json") : null;
        // With expanded, the tour comes with its sections written out, as the Replay page plays it in place; with inPlace
        // alone it is played in place but comes as tour.json has it, for the page to write out section by section as the
        // Tour page does, so its editor works the same on both.
        // What is at a candle, as the chart had it at the cursor (seen): the checklist the Replay page's candle card shows.
        // greenRuns and redRuns are the page's Successive Candles settings: how long a run must be to count.
        app.MapGet("/api/explain/at", (string dataset, string at, string? seen, int? greenRuns, int? redRuns) =>
        {
            if (!byId.TryGetValue(dataset, out var data))
                return Results.NotFound();
            var options = new ExplainOptions(greenRuns ?? ExplainOptions.Default.GreenRuns, redRuns ?? ExplainOptions.Default.RedRuns);
            return Results.Text(new JsonArray(Explain.At(data.Prices, at, seen, options, MaxLevel).Select(x => (JsonNode)x.ToJson()).ToArray()).ToJsonString(), "application/json");
        });

        // things lists what to explain, by id as /api/explain/at gives them ("Candle,SuccessiveCandles,Swing:1"), a chapter
        // each; explain and level name one thing the old way. A thing not at the candle is left out and named in missing.
        app.MapGet("/api/explain/tour", (string dataset, string at, string? things, string? explain, int? level, string? seen, bool? expanded, bool? inPlace, int? greenRuns, int? redRuns, string? strategy) =>
        {
            if (!byId.TryGetValue(dataset, out var data))
                return Results.NotFound();
            var ids = things != null ? things.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() : [$"{explain}{(explain is "Candle" or "SuccessiveCandles" ? "" : $":{level ?? 1}")}"];
            var unknown = ids.Where(x => Explain.Thing.Parse(x) == null).ToList();
            if (unknown.Count > 0)
                return Results.BadRequest($"\"{string.Join(", ", unknown)}\" is not a thing the tour can explain: {string.Join(", ", Explain.Terms)}.");
            var options = new ExplainOptions(greenRuns ?? ExplainOptions.Default.GreenRuns, redRuns ?? ExplainOptions.Default.RedRuns);
            // A Trade is a strategy's, which the address gives as JSON.
            Strategy? traded = null;
            if (strategy != null && (traded = Explain.StrategyOf(JsonNode.Parse(strategy), out var wrong)) == null)
                return Results.BadRequest(wrong);
            var def = Explain.Tour(ids.Select(x => Explain.Thing.Parse(x)!).ToList(), data.Id, data.Prices, at, seen, out var missing, inPlace ?? expanded == true, options, traded);
            if (def == null)
                return Results.BadRequest($"Nothing of that is at the candle at {at}: {string.Join(", ", missing)}.");
            if (missing.Count > 0)
                def["missing"] = new JsonArray(missing.Select(x => (JsonNode)x).ToArray());
            if (tourFile != null && File.Exists(tourFile) && JsonNode.Parse(File.ReadAllText(tourFile))?["glossary"] is { } glossary)
                def["glossary"] = glossary.DeepClone();
            var written = video != null || expanded == true ? Tours.Compile(def, byDataset).Expanded : null;
            if (video != null && written != null)
                video.Narrate(written.ToJsonString());
            return Results.Text((expanded == true ? written ?? def : def).ToJsonString(), "application/json");
        });

        // The Trends like the one a written tour explains, for its Find similar: the Trend as the tour shows it, and every
        // Trend the dataset has as a whole, at every level, which the page matches against what is ticked. The list is
        // worked out once for a dataset.
        var allTrends = new ConcurrentDictionary<string, Lazy<string>>();
        app.MapGet("/api/explain/similar", (string dataset, string at, int? level, string? seen, bool? inPlace) =>
        {
            if (!byId.TryGetValue(dataset, out var data))
                return Results.NotFound();
            var shown = Explain.TrendShown(data.Prices, at, level ?? 1, seen, inPlace == true);
            if (shown == null)
                return Results.BadRequest($"No Trend at level {level ?? 1} has the candle at {at} in it.");
            var trends = allTrends.GetOrAdd(data.Id, _ => new Lazy<string>(() => Explain.AllTrends(data.Prices, MaxLevel).ToJsonString())).Value;
            return Results.Text($"{{\"shown\":{shown.ToJsonString()},\"trends\":{trends}}}", "application/json");
        });

        // A strategy run over a whole dataset, as the Strategies page sends it with the dataset's id: every trade it made and
        // their totals. Each run is also written to artifacts/backtests as JSON (the strategy, the totals and the trades) and
        // as CSV (the trades), named by the strategy and the dataset, so the trades of each variation are kept to compare.
        var backtestsDir = Path.GetFullPath(Path.Combine(siteDir, "..", "backtests"));
        // The roles of the markets' periods, kept beside strategies.json, with their log beside the runs; none without the
        // source folder, so nothing is held back.
        var roles = Directory.Exists(sourceDir) ? new PeriodRoles(Path.Combine(sourceDir, "periods.json"), Path.Combine(backtestsDir, "periods.log.jsonl")) : null;

        // The markets strategies are tested on, each with its years: their candles, and their roles with the baselines run
        // on each Test year so far.
        app.MapGet("/api/markets", () =>
        {
            var counts = roles?.TestRuns() ?? [];
            return Results.Json(Markets.All.Where(x => byId.ContainsKey(x.Id)).Select(market =>
            {
                var prices = byId[market.Id].Prices;
                var years = Enumerable.Range(prices[0].DateTime.Year, prices[^1].DateTime.Year - prices[0].DateTime.Year + 1).Select(year =>
                {
                    var (first, end) = StrategyBacktest.Window(prices, new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                    return new
                    {
                        Year = year, Candles = end - first, First = prices[first].DateTime, Last = prices[end - 1].DateTime,
                        Role = (roles?.Role(market.Group, year) ?? EnumPeriodRole.Search).ToString().ToLowerInvariant(),
                        TestRuns = counts.GetValueOrDefault((market.Group, year)),
                    };
                }).Where(x => x.Candles > 0);
                return new { market.Id, market.Name, market.Group, market.Hours, Years = years };
            }), Json);
        });

        // A period's role changed, { group, year, role }, kept and logged at once.
        app.MapPut("/api/periods", async (HttpRequest request) =>
        {
            if (roles == null)
                return Results.NotFound("The server has no source folder to keep the roles in.");
            var body = await JsonNode.ParseAsync(request.Body);
            var group = body?["group"]?.GetValue<string>();
            var year = body?["year"]?.GetValue<int>();
            if (group == null || year == null || Markets.All.All(x => x.Group != group)
                || !Enum.TryParse<EnumPeriodRole>(body?["role"]?.GetValue<string>(), true, out var role))
                return Results.BadRequest("The request is { group, year, role }, with a market group and a role of search, test or locked.");
            roles.Set(group, year.Value, role);
            return Results.Ok();
        });

        app.MapPost("/api/backtest", async (HttpRequest request) =>
        {
            var (strategy, data, save, _, window, error) = await BacktestRequest(request);
            if (error != null)
                return Results.BadRequest(error);
            // The charts ask for a strategy's trades to draw them, and those are not kept, nor held to the periods' roles.
            if (save && roles?.Check(data!.Id, data.Prices, window, "run", strategy!.Id) is { } refused)
                return Results.BadRequest(refused);

            var run = StrategyBacktest.Run(data!.Prices, strategy!, data.Id, window);
            if (!save)
                return Results.Text($"{{\"run\":{JsonSerializer.Serialize(run, StrategyBook.JsonOptions)}}}", "application/json");
            var name = RunPath(strategy!, data, window);
            var json = JsonSerializer.Serialize(run, StrategyBook.JsonOptions);
            await File.WriteAllTextAsync($"{name}.json", json);
            await File.WriteAllTextAsync($"{name}.csv", StrategyBacktest.ToCsv(run.Trades));
            return Results.Text($"{{\"saved\":{JsonSerializer.Serialize($"{name}.json")},\"run\":{json}}}", "application/json");
        });

        // Find similar on a trade: every candle of the dataset with what the strategy's entry looks for, found another way
        // than the backtest does (see StrategyBacktest.Occurrences), for the page to check the trades against.
        app.MapPost("/api/backtest/similar", async (HttpRequest request) =>
        {
            var (strategy, data, _, _, _, error) = await BacktestRequest(request);
            if (error != null)
                return Results.BadRequest(error);
            var found = StrategyBacktest.Occurrences(data!.Prices, strategy!);
            return Results.Json(new { Candles = found.Select(i => new { Index = i, Time = data.Prices[i].DateTime, Measurable = StrategyBacktest.Measurable(data.Prices, i, strategy) }) }, Json);
        });

        // The random-entry baseline of a strategy (see StrategyBaseline): { dataset, strategy, from, to, runs, seed, save },
        // all but the dataset and strategy optional. It is saved beside the strategy's run in artifacts/backtests, as JSON
        // ending .baseline.json.
        app.MapPost("/api/backtest/baseline", async (HttpRequest request) =>
        {
            var (strategy, data, save, body, window, error) = await BacktestRequest(request);
            if (error != null)
                return Results.BadRequest(error);
            if (roles?.Check(data!.Id, data.Prices, window, "baseline", strategy!.Id) is { } refused)
                return Results.BadRequest(refused);
            BaselineRun baseline;
            try
            {
                baseline = StrategyBaseline.Run(data!.Prices, strategy!, data.Id, body?["runs"]?.GetValue<int>() ?? StrategyBaseline.DefaultRuns,
                    body?["seed"]?.GetValue<int>() ?? 1, window);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(e.Message);
            }

            var json = JsonSerializer.Serialize(baseline, StrategyBook.JsonOptions);
            if (!save)
                return Results.Text($"{{\"baseline\":{json}}}", "application/json");
            var name = $"{RunPath(strategy!, data!, window)}.baseline.json";
            await File.WriteAllTextAsync(name, json);
            return Results.Text($"{{\"saved\":{JsonSerializer.Serialize(name)},\"baseline\":{json}}}", "application/json");
        });

        // A strategy swept over the numbers it varies (see StrategySweep): { dataset, strategy, from, to, runs, seed, save }, as
        // for a baseline, each variation with a baseline of its own; on Search periods only. It is saved beside the strategy's
        // runs, ending .sweep.json.
        app.MapPost("/api/backtest/sweep", async (HttpRequest request) =>
        {
            var (strategy, data, save, body, window, error) = await BacktestRequest(request);
            if (error != null)
                return Results.BadRequest(error);
            if (roles?.Check(data!.Id, data.Prices, window, "sweep", strategy!.Id) is { } refused)
                return Results.BadRequest(refused);
            SweepRun sweep;
            try
            {
                sweep = StrategySweep.Run(data!.Prices, strategy!, data.Id, body?["runs"]?.GetValue<int>() ?? StrategyBaseline.DefaultRuns,
                    body?["seed"]?.GetValue<int>() ?? 1, window);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(e.Message);
            }

            var json = JsonSerializer.Serialize(sweep, StrategyBook.JsonOptions);
            if (!save)
                return Results.Text($"{{\"sweep\":{json}}}", "application/json");
            var name = $"{RunPath(strategy!, data!, window)}.sweep.json";
            await File.WriteAllTextAsync(name, json);
            return Results.Text($"{{\"saved\":{JsonSerializer.Serialize(name)},\"sweep\":{json}}}", "application/json");
        });

        // Where a strategy's runs over a dataset are written, without the extension: named by the strategy and the dataset,
        // and the dates of the window when it is not the whole dataset, so each period's runs are kept.
        string RunPath(Strategy strategy, Dataset data, (int First, int End) window)
        {
            Directory.CreateDirectory(backtestsDir);
            var dates = window == (0, data.Prices.Count) || window.End <= window.First ? ""
                : $"_{data.Prices[window.First].DateTime:yyyyMMdd}-{data.Prices[window.End - 1].DateTime:yyyyMMdd}";
            return Path.Combine(backtestsDir, $"{string.Concat(strategy.Id.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c))}@{data.Id}{dates}");
        }

        // A backtest request, { dataset, strategy, from, to, save }: the strategy, the dataset and the window of it from the
        // date from up to but not including the date to (all of it without them), or what is wrong with them, and the
        // request's body for whatever else it carries.
        async Task<(Strategy? Strategy, Dataset? Data, bool Save, JsonNode? Body, (int First, int End) Window, string? Error)> BacktestRequest(HttpRequest request)
        {
            try
            {
                var body = await JsonNode.ParseAsync(request.Body);
                var strategy = body?["strategy"]?.Deserialize<Strategy>(StrategyBook.JsonOptions);
                var dataset = body?["dataset"]?.GetValue<string>();
                if (strategy == null || dataset == null || !byId.TryGetValue(dataset, out var data))
                    return (null, null, false, null, default, "The request is { dataset, strategy }, with a dataset the server has.");
                var errors = strategy.Validate();
                if (errors.Count > 0)
                    return (null, null, false, null, default, string.Join(" ", errors));
                DateTime? Date(string key) => body?[key]?.GetValue<string>() is { Length: > 0 } text
                    ? DateTime.Parse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal)
                    : null;
                var window = StrategyBacktest.Window(data.Prices, Date("from"), Date("to"));
                if (window.End <= window.First)
                    return (null, null, false, null, default, "The dataset has no candles from the date from up to the date to.");
                return (strategy, data, body?["save"]?.GetValue<bool>() ?? true, body, window, null);
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
            {
                return (null, null, false, null, default, $"The request is not a backtest: {e.Message}");
            }
        }

        // The tour's editor on the page saves the tour back to its source file. The page says which version of the file it
        // started from, so a file that has changed since, as when it is edited by hand, is not written over.
        if (Directory.Exists(sourceDir))
        {
            var tourPath = Path.Combine(sourceDir, "tour.json");

            // The tour as the page plays it, with every section that explains something written out, for the narration tool.
            string? ExpandedTour()
            {
                if (!File.Exists(tourPath) || JsonNode.Parse(File.ReadAllText(tourPath)) is not { } def)
                    return null;
                return (Tours.Compile(def, byDataset, teachOnce: true).Expanded ?? def).ToJsonString();
            }

            app.MapGet("/api/tour", () => ExpandedTour() is { } tour ? Results.Text(tour, "application/json") : Results.NotFound());

            // The definitions are read from the source copy, which the editor saves, so a change is used at once. Saving
            // works as the tour's does: the page says which version it started from, and a file changed since is kept.
            var definitionsPath = Path.Combine(sourceDir, "definitions.json");
            Definitions.UseFile(definitionsPath);
            app.MapPut("/api/definitions", async (HttpRequest request) =>
            {
                using var reader = new StreamReader(request.Body);
                var text = await reader.ReadToEndAsync();
                try
                {
                    using var parsed = JsonDocument.Parse(text);
                    if (parsed.RootElement.ValueKind != JsonValueKind.Object || parsed.RootElement.EnumerateObject().Any(x => x.Value.ValueKind != JsonValueKind.String))
                        return Results.BadRequest("The definitions are an object of texts by key.");
                }
                catch (JsonException)
                {
                    return Results.BadRequest("The definitions are not valid JSON.");
                }

                if (File.Exists(definitionsPath) && request.Headers["X-Definitions-Base"].ToString() != TextKey(await File.ReadAllTextAsync(definitionsPath)))
                    return Results.Conflict("definitions.json has changed on disk since the page loaded it.");

                await File.WriteAllTextAsync(definitionsPath, text);
                // The texts that cite a definition changed with it, so they are read aloud again.
                ReadAloud();
                return Results.NoContent();
            });
            if (video != null)
                video.TourSource = ExpandedTour;
            app.MapPut("/api/tour", async (HttpRequest request) =>
            {
                using var reader = new StreamReader(request.Body);
                var text = await reader.ReadToEndAsync();
                try
                {
                    using var parsed = JsonDocument.Parse(text);
                }
                catch (JsonException)
                {
                    return Results.BadRequest("The tour is not valid JSON.");
                }

                if (File.Exists(tourPath) && request.Headers["X-Tour-Base"].ToString() != TextKey(await File.ReadAllTextAsync(tourPath)))
                    return Results.Conflict("tour.json has changed on disk since the page loaded it.");

                await File.WriteAllTextAsync(tourPath, text);
                // The texts that changed are read aloud at once, so Voice has their clips by the time they are played.
                ReadAloud();
                return Results.NoContent();
            });

            // The Strategies page saves strategies.json as the tour's editor saves tour.json, refused when the file has
            // changed on disk since the page loaded it.
            var strategiesPath = Path.Combine(sourceDir, "strategies.json");
            app.MapPut("/api/strategies", async (HttpRequest request) =>
            {
                using var reader = new StreamReader(request.Body);
                var text = await reader.ReadToEndAsync();
                try
                {
                    StrategyBook.Parse(text);
                }
                catch (JsonException e)
                {
                    return Results.BadRequest($"The strategies are not valid: {e.Message}");
                }

                if (File.Exists(strategiesPath) && request.Headers["X-Strategies-Base"].ToString() != TextKey(await File.ReadAllTextAsync(strategiesPath)))
                    return Results.Conflict("strategies.json has changed on disk since the page loaded it.");

                await File.WriteAllTextAsync(strategiesPath, text);
                return Results.NoContent();
            });

            // An analysis's own edits: its sections as changed for that analysis alone, by the thing and detail each tells
            // of, laid over the sections written from the prices each time it opens. They are kept in a file of their own,
            // named from the analysis's key (its candle, the cursor it was seen from and its settings) and holding the key,
            // so no other analysis reads them.
            var editsDir = Path.Combine(sourceDir, "analysis-edits");
            string EditsPath(string key) => Path.Combine(editsDir, $"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16].ToLowerInvariant()}.json");
            app.MapGet("/api/analysis/edits", (string key) =>
            {
                var path = EditsPath(key);
                var kept = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null;
                return kept?["key"]?.GetValue<string>() == key
                    ? Results.Text(kept.ToJsonString(), "application/json")
                    : Results.Json(new JsonObject { ["key"] = key, ["sections"] = new JsonObject() }, Json);
            });
            app.MapPut("/api/analysis/edits", async (string key, HttpRequest request) =>
            {
                JsonObject? edits;
                try
                {
                    edits = (await JsonNode.ParseAsync(request.Body))?["sections"] as JsonObject;
                }
                catch (JsonException)
                {
                    return Results.BadRequest("The edits are not valid JSON.");
                }

                if (edits == null || edits.Any(x => x.Value is not JsonObject))
                    return Results.BadRequest("The edits are { \"sections\": { \"<thing>|<detail>\": { the section's settings changed } } }.");

                var path = EditsPath(key);
                // No edits left: the analysis is as written from the prices again, and its file goes.
                if (edits.Count == 0)
                {
                    File.Delete(path);
                    return Results.NoContent();
                }

                Directory.CreateDirectory(editsDir);
                await File.WriteAllTextAsync(path, new JsonObject { ["key"] = key, ["sections"] = edits.DeepClone() }.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                return Results.NoContent();
            });

            // The page asks for the tour's texts to be read when it opens the tour or turns Voice on, so clips missing for
            // whatever reason are made without a button being pressed.
            app.MapPost("/api/narrate", () =>
            {
                ReadAloud();
                return video != null ? Results.Json(video.Status(), Json) : Results.NoContent();
            });

            void ReadAloud()
            {
                if (video != null && ExpandedTour() is { } tour)
                    video.Narrate(tour);
            }
        }

        // The tour's Update audio and Generate video buttons run tools/tour-video, and its Download button fetches the video
        // that made.
        if (video != null)
        {
            app.MapGet("/api/video", () => Results.Json(video.Status(), Json));
            app.MapPost("/api/video", (bool? audio) =>
            {
                video.Start(audio == true);
                return Results.Json(video.Status(), Json);
            });
            app.MapGet("/api/video/file", () => File.Exists(video.VideoPath)
                ? Results.File(video.VideoPath, "video/mp4", "tour.mp4")
                : Results.NotFound());

            // An analysis's Make video records it by its address on the page, and Download video fetches the last one made.
            app.MapPost("/api/video/analysis", async (HttpRequest request) =>
            {
                var address = (await JsonNode.ParseAsync(request.Body))?["address"]?.GetValue<string>();
                if (address == null || !address.StartsWith("#Analysis?"))
                    return Results.BadRequest("The request is { address }, an analysis's address on the page (#Analysis?...).");
                video.RecordAnalysis(address);
                return Results.Json(video.Status(), Json);
            });
            app.MapGet("/api/video/analysis/file", () => File.Exists(video.AnalysisVideoPath)
                ? Results.File(video.AnalysisVideoPath, "video/mp4", "analysis.mp4")
                : Results.NotFound());
        }

        if (video != null)
            app.Lifetime.ApplicationStopping.Register(video.Dispose);
        Console.WriteLine($"Serving {siteDir} with replay for {string.Join(", ", datasets.Select(x => x.Id))} on http://localhost:{port}");
        await app.RunAsync();
    }

    /// <summary>
    /// The page's short hash of a text (FNV-1a, 32 bits, over its UTF-16 units), which names the version of tour.json it holds.
    /// </summary>
    private static string TextKey(string text)
    {
        var hash = 0x811c9dc5u;
        foreach (var unit in text)
            hash = unchecked((hash ^ unit) * 0x01000193u);
        return hash.ToString("x8");
    }

    /// <summary>
    /// One batch: the timeline for candles start to start + BatchSize, plus the candle before so items already holding at the
    /// start are known to be carried over rather than new. The candles are counted from the anchor.
    /// </summary>
    private static string Batch(Dataset data, int anchor, int start, (double Retracement, double Band) settings)
    {
        var prices = anchor == 0 ? data.Prices : data.Prices.GetRange(anchor, data.Prices.Count - anchor);
        var to = Math.Min(start + BatchSize, prices.Count);
        var timeline = MarketStructure.Timeline(prices, EnumPriceBasis.Close, MaxLevel, from: Math.Max(0, start - 1), to: to,
            keepFrom: Math.Max(0, start - Window), minRetracement: settings.Retracement, band: settings.Band);
        return JsonSerializer.Serialize(new { Anchor = anchor, From = start, To = to, Timeline = timeline }, Json);
    }

    /// <summary>
    /// The timeline of the last candle alone, keeping everything from the first: the structure of the whole dataset in hindsight.
    /// </summary>
    private static byte[] Complete(Dataset data, (double Retracement, double Band) settings)
    {
        var last = data.Prices.Count - 1;
        var timeline = MarketStructure.Timeline(data.Prices, EnumPriceBasis.Close, MaxLevel, from: last, to: last + 1, keepFrom: 0,
            minRetracement: settings.Retracement, band: settings.Band);
        return JsonSerializer.SerializeToUtf8Bytes(new { Anchor = 0, From = last, To = last + 1, Timeline = timeline }, Json);
    }
}
