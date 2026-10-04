using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
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
                return Results.BadRequest("The request is { dataset, anchor, from, where, section }.");

            var first = Math.Clamp(body["anchor"]?.GetValue<int>() ?? 0, 0, data.Prices.Count - 1);
            var errors = new List<string>();
            var expanded = Explain.Expand(section, data.Prices.GetRange(first, data.Prices.Count - first), body["from"]?.GetValue<int>() ?? 0,
                body["where"]?.GetValue<string>() ?? "the section", errors);
            return Results.Json(new { Section = expanded, Errors = errors }, Json);
        });

        // A tour of its own around one thing, for the Tour page opened with ?explain=Swing&dataset=btc-1h&at=2023-01-03T00:00&level=1.
        // seen is the Replay page's cursor, so the Swing or Trend is the one the page showed there. The written tour takes
        // the glossary of the tour in tour.json, so the voice says BoS and MSB as that does, and its texts are read aloud in
        // the background at once, so the clips are there by the time Voice is pressed.
        var byDataset = datasets.ToDictionary(x => x.Id, x => (IReadOnlyList<Price>)x.Prices);
        var tourFile = sourceDir != null ? Path.Combine(sourceDir, "tour.json") : null;
        app.MapGet("/api/explain/tour", (string explain, string dataset, string at, int? level, string? seen) =>
        {
            if (!byId.TryGetValue(dataset, out var data))
                return Results.NotFound();
            if (!Explain.Terms.Contains(explain))
                return Results.BadRequest($"\"{explain}\" is not a thing the tour can explain: {string.Join(", ", Explain.Terms)}.");
            var def = Explain.Tour(explain, data.Id, data.Prices, at, level ?? 1, seen);
            if (def == null)
                return Results.BadRequest($"No {explain} at level {level ?? 1} has the candle at {at} in it.");
            if (tourFile != null && File.Exists(tourFile) && JsonNode.Parse(File.ReadAllText(tourFile))?["glossary"] is { } glossary)
                def["glossary"] = glossary.DeepClone();
            if (video != null && Tours.Compile(def, byDataset).Expanded is { } expanded)
                video.Narrate(expanded.ToJsonString());
            return Results.Text(def.ToJsonString(), "application/json");
        });

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
                return (Tours.Compile(def, byDataset).Expanded ?? def).ToJsonString();
            }

            app.MapGet("/api/tour", () => ExpandedTour() is { } tour ? Results.Text(tour, "application/json") : Results.NotFound());
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
}
