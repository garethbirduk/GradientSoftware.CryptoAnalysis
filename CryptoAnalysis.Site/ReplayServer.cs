using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gradient.CryptoAnalysis.Research;
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
/// A fetching of months of a market from its source, as it goes: how many of its months are done, the one being fetched,
/// the candles fetched and how many of them were new, the years it Locked, and how it ended: of itself, cancelled, at the
/// start of the source's history (Began), or with an error.
/// </summary>
public sealed class DataJob(string market, string source, int total)
{
    /// <summary>
    /// The hours a month's fetch must add to a year that already has candles for the year to become Locked: a week's worth,
    /// so that new stretches are kept back and a few missing hours filled in are not.
    /// </summary>
    public const int LockAt = 168;

    public string Market { get; } = market;
    public string Source { get; } = source;
    public int Total { get; } = total;
    public int Done { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int Fetched { get; set; }
    public int Gained { get; set; }
    public List<int> Locked { get; } = [];
    public bool Running { get; set; } = true;
    public bool Cancelled { get; set; }
    public bool Began { get; set; }
    public string? Error { get; set; }
}

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

    // How far past the last candle asked for a series from a later anchor is read, and how many series are held besides
    // the datasets' own.
    private const int StructureAhead = 2000;
    private const int StructuresHeld = 6;

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
        var batches = new ConcurrentDictionary<(string Dataset, int Anchor, int Start, (double Retracement, double Band) Settings, int RangeSwings, bool DeepBreaks, bool Overlaps), Lazy<string>>();
        // What is slow to work out is kept on disk too, beside the site, so it is there after a restart: the replay's batches,
        // and the datasets' structure.
        var kept = new StructureCache(Path.GetFullPath(Path.Combine(siteDir, "..", "cache")));
        kept.Prune();

        // The structure of each series replayed, read once and held: what was known at any candle is looked up in it, and
        // the Replay and Chart pages' timelines are made from it. A series is a dataset from an anchor on, with the range
        // settings its ranges are found by. A dataset from its first candle is read whole; one from a later anchor, as a
        // tour's scene is, only some way past the last candle asked for, and further when more is. A dataset's own is kept
        // on disk too, as reading every candle of a long series takes a few seconds, and read back from there while its
        // candles are the same. A dataset whose candles have changed is read again, and beyond a few the series least
        // lately asked for are dropped, the datasets' own never. Those read with the usual range settings are the ones
        // a tour or an analysis looks its structure up in (see StructureStores).
        var defaults = (Retracement: Ranges.DefaultMinRetracement, Band: Ranges.DefaultBand);
        var structures = new Dictionary<(string Dataset, int Anchor, (double Retracement, double Band) Settings), HeldStructure>();
        var asked = 0L;
        StructureStore Structure(Dataset data, int anchor = 0, (double Retracement, double Band)? ranges = null, int upTo = 0)
        {
            var settings = ranges ?? defaults;
            var key = (data.Id, anchor, settings);
            HeldStructure? held;
            lock (structures)
            {
                var need = anchor == 0 ? data.Prices.Count : Math.Min(upTo, data.Prices.Count - anchor);
                if (!structures.TryGetValue(key, out held) || !ReferenceEquals(held.Of, data.Prices) || held.Candles < need)
                {
                    held?.Drop();
                    var count = anchor == 0 ? need : Math.Min(data.Prices.Count - anchor, Math.Max(need + StructureAhead, (held?.Candles ?? 0) * 2));
                    var prices = anchor == 0 ? data.Prices : data.Prices.GetRange(anchor, count);
                    structures[key] = held = new HeldStructure(data.Prices, count, new Lazy<StructureStore>(() =>
                    {
                        var store = anchor == 0 ? ReadOrKept(kept, data, settings) : StructureStore.Read(prices, EnumPriceBasis.Close, settings.Retracement, settings.Band);
                        if (settings == defaults)
                            StructureStores.Hold(store);
                        return store;
                    }));
                }

                held.Asked = ++asked;
                foreach (var old in structures.Where(x => x.Key.Anchor != 0 || x.Key.Settings != defaults).OrderByDescending(x => x.Value.Asked).Skip(StructuresHeld).ToList())
                {
                    old.Value.Drop();
                    structures.Remove(old.Key);
                }
            }

            return held.Store.Value;
        }

        var held = datasets.ToList();
        _ = Task.Run(() => held.ForEach(x => Structure(x)));

        // A strategy's steps and the explorer's conditions read a dataset's structure from the same store.
        KnownStructure.Stores = (prices, retracement, band) =>
            datasets.FirstOrDefault(x => ReferenceEquals(x.Prices, prices)) is { } data ? Structure(data, 0, (retracement, band)) : null;

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

        // The structure of a stretch of candles, from and to counted from the dataset's first (the last 720 when neither is
        // given), as known at the close of a candle (at; the last when not given), at levels up to maxLevel: looked up in
        // the dataset's structure store, each item with its id and as it stood then. The candidates are all of those then.
        app.MapGet("/api/structure", (string dataset, int? from, int? to, int? at, int? maxLevel) =>
        {
            if (!byId.TryGetValue(dataset, out var data))
                return Results.NotFound();

            var store = Structure(data);
            var last = store.Candles - 1;
            var known = Math.Clamp(at ?? last, 0, last);
            var end = Math.Clamp(to ?? known, 0, last);
            var start = Math.Clamp(from ?? end - 719, 0, end);
            return Results.Json(new
            {
                store.Candles,
                From = start,
                To = end,
                At = known,
                Pivots = store.Pivots(start, end, known, maxLevel ?? int.MaxValue).Select(x => new { x.Id, x.Candle, Time = data.Prices[x.Candle].DateTime, x.Kind, x.Price, x.Level, x.Known }),
                Swings = store.Swings(start, end, known, maxLevel ?? int.MaxValue),
                Breaks = store.Breaks(start, end, known, maxLevel ?? int.MaxValue),
                Trends = store.Trends(start, end, known, maxLevel ?? int.MaxValue),
                Retracements = store.Retracements(start, end, known, maxLevel ?? int.MaxValue),
                Candidates = store.Candidates(known, maxLevel ?? int.MaxValue),
                Ranges = store.Ranges(start, end, known, maxLevel ?? int.MaxValue),
            }, Json);
        });

        // With an anchor, the replay is of the dataset from that candle on: from counts from it, and so does the batch. The
        // range settings (the least retracement and the band, as percentages) are the page's, so each pair has batches of its own.
        app.MapGet("/api/replay", (string dataset, int from, int? anchor, double? retracement, double? band, int? rangeSwings, bool? deepBreaks, bool? overlaps) =>
        {
            if (!byId.TryGetValue(dataset, out var data))
                return Results.NotFound();

            var first = Math.Clamp(anchor ?? 0, 0, data.Prices.Count - 1);
            var start = Math.Clamp(from / BatchSize * BatchSize, 0, data.Prices.Count - first - 1);
            var settings = (Retracement: retracement ?? Ranges.DefaultMinRetracement, Band: band ?? Ranges.DefaultBand);
            var json = batches.GetOrAdd((dataset, first, start, settings, Math.Clamp(rangeSwings ?? 0, 0, 99), deepBreaks == true, overlaps == true), key => new Lazy<string>(() =>
                kept.Text(BatchName(data, key.Anchor, key.Start, key.Settings, key.RangeSwings, key.DeepBreaks, key.Overlaps), () =>
                {
                    var to = Math.Min(key.Start + BatchSize, data.Prices.Count - key.Anchor);
                    return Batch(Structure(data, key.Anchor, key.Settings, to), key.Anchor, key.Start, to, key.RangeSwings, key.DeepBreaks, key.Overlaps);
                }))).Value;
            return Results.Text(json, "application/json");
        });

        // The dataset as known at its last candle: what the Chart page shows. With from and to, only what lies at those
        // candles, and with narrowest, leaving out what is narrower than that many candles, as the page asks for the
        // stretch it draws and what can be seen at its zoom; looked up each time. With neither, everything in it, which is
        // kept as UTF-8, as a long dataset's comes to tens of megabytes.
        var completes = new ConcurrentDictionary<(string Dataset, (double Retracement, double Band) Settings, int RangeSwings, bool DeepBreaks, bool Overlaps), Lazy<byte[]>>();
        // With at, the stretch is as known at that candle, and with maxLevel only the levels to that one: what an analysis
        // seen from a candle shows of a thing that began long before it, which the replay's batches do not go back to.
        app.MapGet("/api/complete", (string dataset, double? retracement, double? band, int? from, int? to, int? narrowest, int? at, int? maxLevel, int? rangeSwings, bool? deepBreaks, bool? overlaps) =>
        {
            if (!byId.TryGetValue(dataset, out var data))
                return Results.NotFound();

            var settings = (Retracement: retracement ?? Ranges.DefaultMinRetracement, Band: band ?? Ranges.DefaultBand);
            if (from != null || to != null || narrowest != null)
            {
                var store = Structure(data, 0, settings);
                var last = Math.Clamp(at ?? store.Candles - 1, 0, store.Candles - 1);
                var (first, end) = (Math.Clamp(from ?? 0, 0, last), Math.Clamp(to ?? last, 0, last));
                var timeline = MarketStructure.Complete(store, Math.Clamp(maxLevel ?? MaxLevel, 0, MaxLevel), first, end, Math.Max(0, narrowest ?? 0), last, Math.Clamp(rangeSwings ?? 0, 0, 99), deepBreaks == true, overlaps == true);
                return Results.Bytes(JsonSerializer.SerializeToUtf8Bytes(new { Anchor = 0, From = last, To = last + 1, Stretch = new { From = first, To = end, Narrowest = Math.Max(0, narrowest ?? 0) }, Timeline = timeline }, Json), "application/json");
            }

            var json = completes.GetOrAdd((dataset, settings, Math.Clamp(rangeSwings ?? 0, 0, 99), deepBreaks == true, overlaps == true), key => new Lazy<byte[]>(() => Complete(Structure(data, 0, key.Settings), key.RangeSwings, key.DeepBreaks, key.Overlaps))).Value;
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
            {
                // The series is read as far as the event is looked for, for it to be looked up.
                Structure(data, k.Item2, null, k.Item3 + Tours.EventSearchLimit + 1);
                return Tours.FindEvent(data.Prices.GetRange(k.Item2, data.Prices.Count - k.Item2), k.Item3, k.Item4, k.Item5, k.Item6);
            })).Value;
            return Results.Json(new { Index = index }, Json);
        });

        // A scene's prices, a dataset's from its anchor on, are kept as one list for each: what is worked out once for a
        // series and kept with it, as a strategy's market structure is (see KnownStructure), is then worked out once a scene.
        // A scene's own structure store is read by KnownStructure, as the server holds only the datasets' for it.
        var scenes = new ConcurrentDictionary<(string, int), List<Price>>();
        List<Price> SceneOf(Dataset data, int first) => first == 0 ? data.Prices : scenes.GetOrAdd((data.Id, first), k => data.Prices.GetRange(k.Item2, data.Prices.Count - k.Item2));

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
            // The scene's series is read whole, for what the section tells of to be looked up in its store.
            Structure(data, first, null, data.Prices.Count - first);
            var expanded = Explain.Expand(section, SceneOf(data, first), body["from"]?.GetValue<int>() ?? 0,
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
            return Results.Text(new JsonArray(Explain.At(data.Prices, at, seen, Structure(data), options, MaxLevel).Select(x => (JsonNode)x.ToJson()).ToArray()).ToJsonString(), "application/json");
        });

        // things lists what to explain, by id as /api/explain/at gives them ("Candle,SuccessiveCandles,Swing:1"), a chapter
        // each; explain and level name one thing the old way. A thing not at the candle is left out and named in missing.
        app.MapGet("/api/explain/tour", (string dataset, string at, string? things, string? explain, int? level, string? seen, bool? expanded, bool? inPlace, int? greenRuns, int? redRuns, string? strategy, int? rangeSwings) =>
        {
            if (!byId.TryGetValue(dataset, out var data))
                return Results.NotFound();
            var ids = things != null ? things.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() : [$"{explain}{(explain is "Candle" or "SuccessiveCandles" ? "" : $":{level ?? 1}")}"];
            var unknown = ids.Where(x => Explain.Thing.Parse(x) == null).ToList();
            if (unknown.Count > 0)
                return Results.BadRequest($"\"{string.Join(", ", unknown)}\" is not a thing the tour can explain: {string.Join(", ", Explain.Terms)}.");
            var options = new ExplainOptions(greenRuns ?? ExplainOptions.Default.GreenRuns, redRuns ?? ExplainOptions.Default.RedRuns, Math.Clamp(rangeSwings ?? ExplainOptions.Default.RangeSwings, 1, 99));
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

        // The page's List: where a kind of thing occurs in the whole dataset, as known at its last candle (see Occurrences),
        // with the range settings the page has.
        app.MapGet("/api/occurrences", (string dataset, string kind, double? retracement, double? band, int? rangeSwings) =>
        {
            if (!byId.TryGetValue(dataset, out var data))
                return Results.NotFound();
            var store = Structure(data, 0, (retracement ?? Ranges.DefaultMinRetracement, band ?? Ranges.DefaultBand));
            return Occurrences.Of(store, kind, MaxLevel, Math.Clamp(rangeSwings ?? SwingRanges.DefaultSwings, 1, 99)) is { } rows
                ? Results.Text($"{{\"kind\":{JsonSerializer.Serialize(kind)},\"rows\":{rows.ToJsonString()}}}", "application/json")
                : Results.BadRequest($"\"{kind}\" is not listed: {string.Join(", ", Occurrences.Kinds)}.");
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
        // The Data page: what is held of each hourly market, month by month, and the fetching of a month from a source (see
        // DataStore). A month fetched is read into its market at once, and its caches dropped, so no restart is needed.
        var repoRoot = sourceDir != null ? Path.GetFullPath(Path.Combine(sourceDir, "..", "..")) : null;
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var fetching = new SemaphoreSlim(1, 1);

        void Reload(string group)
        {
            foreach (var market in Markets.All.Where(x => x.Group == group && x.Generate == null))
            {
                var data = new Dataset(market.Id, market.Name, Markets.Load(market, repoRoot!));
                byId[market.Id] = data;
                byDataset[market.Id] = data.Prices;
                if (datasets is List<Dataset> list)
                {
                    var at = list.FindIndex(x => x.Id == market.Id);
                    if (at >= 0)
                        list[at] = data;
                    else
                        list.Add(data);
                }

                foreach (var key in batches.Keys.Where(x => x.Dataset == market.Id))
                    batches.TryRemove(key, out _);
                foreach (var key in completes.Keys.Where(x => x.Dataset == market.Id))
                    completes.TryRemove(key, out _);
                lock (structures)
                {
                    foreach (var old in structures.Where(x => x.Key.Dataset == market.Id).ToList())
                    {
                        old.Value.Drop();
                        structures.Remove(old.Key);
                    }
                }
                foreach (var key in events.Keys.Where(x => x.Item1 == market.Id))
                    events.TryRemove(key, out _);
            }
        }

        app.MapGet("/api/data", () =>
        {
            var sources = sourceDir != null ? DataStore.Sources(sourceDir) : [];
            return Results.Json(Markets.All.Where(x => x.Hours == 1 && x.Generate == null && byId.ContainsKey(x.Id)).Select(market =>
            {
                var source = sources.FirstOrDefault(x => x.Pairs.ContainsKey(market.Group));
                var fetched = repoRoot != null ? DataStore.Held(repoRoot, market.Group) : [];
                var months = byId[market.Id].Prices.GroupBy(x => (x.DateTime.Year, x.DateTime.Month)).Select(x => new
                {
                    x.Key.Year, x.Key.Month, Candles = x.Count(), First = x.Min(p => p.DateTime), Last = x.Max(p => p.DateTime),
                    Fetched = fetched.Where(f => f.Year == x.Key.Year && f.Month == x.Key.Month).Select(f => new { f.Source, f.Candles, f.Whole }),
                });
                // The months a source was asked for and had nothing in, so the page does not offer them again.
                var empty = (repoRoot != null ? DataStore.Empty(repoRoot, market.Group) : []).Select(x => new { x.Year, x.Month, x.Source });
                return new { market.Id, market.Name, market.Group, Source = source == null ? null : new { source.Id, source.Name }, Months = months, Empty = empty };
            }), Json);
        });

        // Fetching runs here, not on the page, so it goes on when the page is reloaded or closed: { market, months: [{ year,
        // month }] } starts a job that fetches the months from the market's source one after another, newest first, each
        // kept and read in as it comes. A Search year becomes Locked when it had no candles before, or when a month adds
        // a good part of a month to it (DataJob.LockAt hours), as nothing has yet been run on what is new in it; a few
        // missing hours filled in a year already run on do not lock it. Two months running, before the oldest held, that the source has nothing in
        // end the job: the source's history begins after them. One job runs at a time; the page asks how it is going
        // (GET api/data/job) and can cancel it (POST api/data/cancel), which stops it after the month being fetched.
        DataJob? job = null;
        CancellationTokenSource? cancel = null;

        app.MapGet("/api/data/job", () => Results.Json(job, Json));

        app.MapPost("/api/data/cancel", () =>
        {
            cancel?.Cancel();
            return Results.Ok();
        });

        app.MapPost("/api/data/fetch", async (HttpRequest request) =>
        {
            var body = await JsonNode.ParseAsync(request.Body);
            var market = Markets.All.FirstOrDefault(x => x.Id == body?["market"]?.GetValue<string>() && x.Hours == 1 && x.Generate == null);
            var now = DateTime.UtcNow;
            var months = (body?["months"]?.AsArray() ?? [])
                .Select(x => (Year: x?["year"]?.GetValue<int>() ?? 0, Month: x?["month"]?.GetValue<int>() ?? 0))
                .Where(x => x.Month is >= 1 and <= 12 && x.Year is >= 1 and <= 9999 && new DateTime(x.Year, x.Month, 1, 0, 0, 0, DateTimeKind.Utc) <= now)
                .Distinct().OrderByDescending(x => x.Year * 12 + x.Month).ToList();
            if (market == null || repoRoot == null || sourceDir == null || months.Count == 0)
                return Results.BadRequest("The request is { market, months: [{ year, month }] }, with an hourly market and months that have begun.");
            var source = DataStore.Sources(sourceDir).FirstOrDefault(x => x.Pairs.ContainsKey(market.Group));
            if (source == null)
                return Results.BadRequest($"No source in data-sources.json has a pair for {market.Group}.");
            if (!await fetching.WaitAsync(0))
                return Results.Conflict("A fetch is already running.");

            var mine = job = new DataJob(market.Id, source.Name, months.Count);
            var stop = cancel = new CancellationTokenSource();
            _ = Task.Run(() => Fetch(mine, stop, market, source, months));
            return Results.Json(mine, Json);
        });

        // Fetches months of a market from its source, newest first, as a job; the one fetch at a time is the caller's to
        // have taken, and is given up when the job ends.
        async Task Fetch(DataJob mine, CancellationTokenSource stop, Market market, DataSource source, List<(int Year, int Month)> months)
        {
            {
                try
                {
                    var oldest = byId.TryGetValue(market.Id, out var held) && held.Prices.Count > 0 ? held.Prices[0].DateTime.Year * 12 + held.Prices[0].DateTime.Month : int.MaxValue;
                    var nothing = 0;
                    foreach (var (year, month) in months)
                    {
                        if (stop.IsCancellationRequested)
                        {
                            mine.Cancelled = true;
                            break;
                        }

                        if (nothing >= 2)
                        {
                            mine.Began = true;
                            break;
                        }

                        mine.Year = year;
                        mine.Month = month;
                        int Held() => byId.TryGetValue(market.Id, out var data) ? data.Prices.Count(x => x.DateTime.Year == year) : 0;
                        var before = Held();
                        var kept = await DataStore.Fetch(http, repoRoot, source, market.Group, year, month, DateTime.UtcNow);
                        Reload(market.Group);
                        var gained = Held() - before;
                        if (gained > 0 && (before == 0 || gained >= DataJob.LockAt) && roles != null && roles.Role(market.Group, year) == EnumPeriodRole.Search)
                        {
                            roles.Set(market.Group, year, EnumPeriodRole.Locked);
                            mine.Locked.Add(year);
                        }

                        mine.Fetched += kept.Count;
                        mine.Gained += gained;
                        nothing = kept.Count == 0 && year * 12 + month < oldest ? nothing + 1 : 0;
                        mine.Done++;
                    }

                    // The structure of what is now held, read once here and not by the first page to ask for it.
                    foreach (var changed in Markets.All.Where(x => x.Group == market.Group && byId.ContainsKey(x.Id)))
                        Structure(byId[changed.Id]);
                }
                catch (Exception e)
                {
                    mine.Error = mine.Year > 0 ? $"{new DateTime(mine.Year, mine.Month, 1):MMM yyyy}: {e.Message}" : e.Message;
                }
                finally
                {
                    mine.Running = false;
                    fetching.Release();
                }
            }
        }

        // On starting, each hourly market that has a source is brought up to date: the months from its last candle's to
        // this one are fetched, one market after another, as jobs the Data page shows and can cancel. It goes on behind the
        // pages, which open on what is held and have the new candles once they are read in. A market already at the last
        // hour that has ended is left, and a source that cannot be reached leaves the market as it is, the job saying why.
        if (repoRoot != null && sourceDir != null)
        {
            _ = Task.Run(async () =>
            {
                foreach (var market in Markets.All.Where(x => x.Hours == 1 && x.Generate == null).ToList())
                {
                    try
                    {
                        var source = DataStore.Sources(sourceDir).FirstOrDefault(x => x.Pairs.ContainsKey(market.Group));
                        if (source == null || !byId.TryGetValue(market.Id, out var data) || data.Prices.Count == 0)
                            continue;
                        var (last, now) = (data.Prices[^1].DateTime, DateTime.UtcNow);
                        // The last candle held is of the hour before the one going on: nothing new has ended.
                        if (last.AddHours(2) > now)
                            continue;
                        var months = new List<(int Year, int Month)>();
                        for (var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc); month.Year * 12 + month.Month >= last.Year * 12 + last.Month; month = month.AddMonths(-1))
                            months.Add((month.Year, month.Month));
                        await fetching.WaitAsync();
                        await Fetch(job = new DataJob(market.Id, source.Name, months.Count), cancel = new CancellationTokenSource(), market, source, months);
                    }
                    catch (Exception e) when (e is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException)
                    {
                        Console.WriteLine($"{market.Name} was not brought up to date: {e.Message}");
                    }
                }
            });
        }

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

        // A round of the game (see StrategyGame): { dataset, strategies, candles, stake, seed }, the strategies as the page
        // has them from strategies.json, two or more. The round comes with the window's candles and some before it as the
        // run-up, the entry conditions reading those. Nothing is saved. A strategy's signals are read once a dataset and kept.
        var gameSignals = new ConcurrentDictionary<(string Dataset, string Strategy), List<(int Index, IReadOnlyList<int> Met)>>();
        app.MapPost("/api/game/round", async (HttpRequest request) =>
        {
            JsonNode? body;
            try
            {
                body = await JsonNode.ParseAsync(request.Body);
            }
            catch (JsonException e)
            {
                return Results.BadRequest($"The request is not a round: {e.Message}");
            }

            var dataset = body?["dataset"]?.GetValue<string>();
            List<Strategy?>? strategies;
            try
            {
                strategies = body?["strategies"]?.AsArray().Select(x => x.Deserialize<Strategy>(StrategyBook.JsonOptions)).ToList();
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException)
            {
                return Results.BadRequest($"The strategies are not strategies: {e.Message}");
            }

            if (dataset == null || !byId.TryGetValue(dataset, out var data) || strategies is not { Count: >= 2 } || strategies.Any(x => x == null))
                return Results.BadRequest("The request is { dataset, strategies, candles, stake }, with a dataset the server has and two or more strategies.");
            var errors = strategies.SelectMany(s => s!.Validate()).ToList();
            if (errors.Count > 0)
                return Results.BadRequest(string.Join(" ", errors));
            var candles = Math.Clamp(body?["candles"]?.GetValue<int>() ?? 48, 1, Math.Max(1, data.Prices.Count));
            var stake = body?["stake"]?.GetValue<double>() ?? 1000;
            var random = body?["seed"]?.GetValue<int>() is { } seed ? new Random(seed) : Random.Shared;
            var round = StrategyGame.Round(data.Prices, data.Id, strategies!, candles, stake, random,
                s => gameSignals.GetOrAdd((data.Id, JsonSerializer.Serialize(s, StrategyBook.JsonOptions)), _ => StrategyBacktest.Signals(data.Prices, s)));
            var before = Math.Min(candles / 2, round.First);
            var shown = data.Prices.Skip(round.First - before).Take(before + candles).ToList();
            var prices = new { T = shown.Select(p => p.DateTime), O = shown.Select(p => p.Open), H = shown.Select(p => p.High), L = shown.Select(p => p.Low), C = shown.Select(p => p.Close) };
            return Results.Text(JsonSerializer.Serialize(new { Round = round, Before = before, Prices = prices }, Json), "application/json");
        });

        // Find similar on a trade: every candle of the dataset with what the strategy's entry looks for, found another way
        // than the backtest does (see StrategyBacktest.Occurrences), for the page to check the trades against.
        app.MapPost("/api/backtest/similar", async (HttpRequest request) =>
        {
            var (strategy, data, _, _, _, error) = await BacktestRequest(request);
            if (error != null)
                return Results.BadRequest(error);
            var found = StrategyBacktest.Occurrences(data!.Prices, strategy!);
            // A target on a Range's lines is set from the Range the entry condition held, which the steps' candles give.
            var met = strategy!.TakeProfit.IsRange || strategy.StopLoss.IsRange ? StrategyBacktest.Signals(data.Prices, strategy).ToDictionary(x => x.Index, x => x.Met) : [];
            return Results.Json(new { Candles = found.Select(i => new { Index = i, Time = data.Prices[i].DateTime, Measurable = StrategyBacktest.Measurable(data.Prices, i, strategy, met.GetValueOrDefault(i)) }) }, Json);
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

        // Conditions measured directly (see Explorer): { cells: [{ dataset, from, to }], sizes, horizon }. For every condition
        // and size, after it, how often the price went up by the size before down by it, against every candle, in each period
        // and all of them pooled. It searches, so it runs on Search periods only.
        app.MapPost("/api/explore", async (HttpRequest request) =>
        {
            JsonNode? body;
            try
            {
                body = await JsonNode.ParseAsync(request.Body);
            }
            catch (JsonException e)
            {
                return Results.BadRequest($"The request is not an exploring of conditions: {e.Message}");
            }

            var sizes = body?["sizes"]?.AsArray().Select(x => x!.GetValue<double>()).Distinct().Order().ToList() ?? [];
            var horizon = body?["horizon"]?.GetValue<int>() ?? 48;
            if (sizes.Count is 0 or > 10 || sizes.Any(x => x is < 0.05 or > 50) || horizon is < 1 or > 2000)
                return Results.BadRequest("The sizes are 1 to 10 percentages from 0.05 to 50, and the horizon 1 to 2,000 candles.");

            var cells = new List<(Dataset Data, (int First, int End) Window)>();
            foreach (var cell in body?["cells"]?.AsArray() ?? [])
            {
                if (!byId.TryGetValue(cell?["dataset"]?.GetValue<string>() ?? "", out var data))
                    return Results.BadRequest("Each cell is { dataset, from, to }, with a dataset the server has.");
                DateTime? Date(string key) => cell?[key]?.GetValue<string>() is { Length: > 0 } text
                    ? DateTime.Parse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal)
                    : null;
                var window = StrategyBacktest.Window(data.Prices, Date("from"), Date("to"));
                if (window.End <= window.First)
                    continue;
                if (roles?.Check(data.Id, data.Prices, window, "explore", "") is { } refused)
                    return Results.BadRequest(refused);
                cells.Add((data, window));
            }

            if (cells.Count is 0 or > 60)
                return Results.BadRequest("Explore 1 to 60 periods that have candles.");

            // Each dataset's conditions and outcomes once, measured in each of its periods.
            var conditions = Research.ExploreConditions.All;
            var met = cells.Select(x => x.Data).Distinct().ToDictionary(x => x.Id, x => conditions.Select(c => c.Test(x.Prices)).ToList());
            var touches = cells.Select(x => x.Data).Distinct().ToDictionary(x => x.Id, x => sizes.Select(s => Research.Explorer.FirstTouch(x.Prices, s, horizon)).ToList());
            var tests = conditions.Count * sizes.Count;
            return Results.Json(new
            {
                Tests = tests,
                Sizes = sizes,
                Horizon = horizon,
                Cells = cells.Select(x => new { Dataset = x.Data.Id, x.Data.Name, From = x.Data.Prices[x.Window.First].DateTime, To = x.Data.Prices[x.Window.End - 1].DateTime, Candles = x.Window.End - x.Window.First }),
                Conditions = conditions.Select((c, k) => new
                {
                    c.Id,
                    c.Name,
                    c.Group,
                    Sizes = sizes.Select((s, n) =>
                    {
                        var each = cells.Select(x => Research.Explorer.Measure(met[x.Data.Id][k], touches[x.Data.Id][n], x.Window)).ToList();
                        var pooled = Research.ConditionStat.Pool(each);
                        return new { Size = s, Pooled = pooled, Corrected = Research.Explorer.Corrected(pooled.Score, tests), Cells = each };
                    }),
                }),
            }, Json);
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
                return (Tours.Compile(def, byDataset, teachOnce: true, Tours.StrategiesOf(sourceDir)).Expanded ?? def).ToJsonString();
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
    /// The name a batch is kept by: its dataset, anchor and range settings (with the count of swings, when ranges are found by swings too), the candles it is of, what the server keeps of
    /// each (the levels and the window), and the fingerprint of every candle it was worked out from, the dataset's up to its
    /// last. A batch stays good as candles are added after it, and is not found again once a candle before it has changed.
    /// </summary>
    public static string BatchName(Dataset data, int anchor, int start, (double Retracement, double Band) settings, int rangeSwings = 0, bool deepBreaks = false, bool overlaps = false)
    {
        var to = Math.Min(start + BatchSize, data.Prices.Count - anchor);
        var number = (double x) => x.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        return Path.Combine("replay", data.Id, $"a{anchor}-r{number(settings.Retracement)}-b{number(settings.Band)}-l{MaxLevel}-w{Window}{(rangeSwings > 0 ? $"-s{rangeSwings}" : "")}{(deepBreaks ? "-d" : "")}{(overlaps ? "-o" : "")}",
            $"{start}-{to}-{PriceFingerprint.Of(data.Prices, anchor + to):x16}.json");
    }

    /// <summary>
    /// One batch: the timeline for candles start up to to, plus the candle before so items already holding at the start are
    /// known to be carried over rather than new, looked up in the structure store of the series. The candles are counted
    /// from the anchor, which the store's are read from.
    /// </summary>
    private static string Batch(StructureStore store, int anchor, int start, int to, int rangeSwings = 0, bool deepBreaks = false, bool overlaps = false)
    {
        var timeline = MarketStructure.Timeline(store, MaxLevel, from: Math.Max(0, start - 1), to: to, keepFrom: Math.Max(0, start - Window), rangeSwings: rangeSwings, deepBreaks: deepBreaks, overlaps: overlaps);
        return JsonSerializer.Serialize(new { Anchor = anchor, From = start, To = to, Timeline = timeline }, Json);
    }

    /// <summary>
    /// The timeline of the last candle alone, keeping everything from the first: the structure of the whole dataset in
    /// hindsight, looked up in its structure store.
    /// </summary>
    private static byte[] Complete(StructureStore store, int rangeSwings = 0, bool deepBreaks = false, bool overlaps = false)
    {
        var last = store.Candles - 1;
        var timeline = MarketStructure.Timeline(store, MaxLevel, from: last, to: last + 1, keepFrom: 0, rangeSwings: rangeSwings, deepBreaks: deepBreaks, overlaps: overlaps);
        return JsonSerializer.SerializeToUtf8Bytes(new { Anchor = 0, From = last, To = last + 1, Timeline = timeline }, Json);
    }

    /// <summary>
    /// A dataset's structure store, read back from where it is kept when it is there, else read from every candle and kept:
    /// named by the dataset, the range settings, and the fingerprint of every candle, so one kept is not found again once
    /// a candle has changed or been added, and is then removed.
    /// </summary>
    private static StructureStore ReadOrKept(StructureCache kept, Dataset data, (double Retracement, double Band) settings)
    {
        var number = (double x) => x.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        var folder = Path.Combine(kept.Folder, "structure", data.Id);
        var name = $"r{number(settings.Retracement)}-b{number(settings.Band)}-";
        var path = Path.Combine(folder, $"{name}{data.Prices.Count}-{PriceFingerprint.Of(data.Prices, data.Prices.Count):x16}.bin");
        try
        {
            if (File.Exists(path))
            {
                using var file = new BufferedStream(File.OpenRead(path), 1 << 20);
                if (StructureStore.Load(file, data.Prices) is { } had)
                    return had;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }

        var store = StructureStore.Read(data.Prices, EnumPriceBasis.Close, settings.Retracement, settings.Band);
        try
        {
            Directory.CreateDirectory(folder);
            // Written beside the file and moved over it, so a reader never has half of one.
            var writing = $"{path}.{Guid.NewGuid():N}.tmp";
            using (var file = new BufferedStream(File.Create(writing), 1 << 20))
                store.Save(file);
            File.Move(writing, path, overwrite: true);
            foreach (var other in Directory.GetFiles(folder, $"{name}*.bin").Where(x => !string.Equals(Path.GetFullPath(x), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)))
                File.Delete(other);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }

        return store;
    }

    // A series' structure store as the server holds it: the dataset's candles it was read from, how many of them from
    // its anchor, and when it was last asked for.
    private sealed class HeldStructure(List<Price> of, int candles, Lazy<StructureStore> store)
    {
        /// <summary>
        /// Stops a tour or an analysis looking its structure up in the store, as it is held no longer.
        /// </summary>
        public void Drop()
        {
            if (store.IsValueCreated)
                StructureStores.Drop(store.Value);
        }

        public List<Price> Of => of;

        public int Candles => candles;

        public Lazy<StructureStore> Store => store;

        public long Asked { get; set; }
    }
}
