using System.Collections.Concurrent;
using System.Text.Json;
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
/// candle of a range, computed from the dataset's first candle. Local only.
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
    /// Runs the server on localhost until stopped.
    /// </summary>
    public static async Task Run(string siteDir, IReadOnlyList<Dataset> datasets, int port)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");
        builder.Services.AddResponseCompression(o => o.Providers.Add<GzipCompressionProvider>());
        var app = builder.Build();
        app.UseResponseCompression();

        var files = new PhysicalFileProvider(siteDir);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        // The site is rebuilt in place, so the browser must check for a newer page and data each time.
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
        });

        var byId = datasets.ToDictionary(x => x.Id);
        var batches = new ConcurrentDictionary<(string, int), Lazy<string>>();

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

        app.MapGet("/api/replay", (string dataset, int from) =>
        {
            if (!byId.TryGetValue(dataset, out var data))
                return Results.NotFound();

            var start = Math.Clamp(from / BatchSize * BatchSize, 0, data.Prices.Count - 1);
            var json = batches.GetOrAdd((dataset, start), key => new Lazy<string>(() => Batch(data, key.Item2))).Value;
            return Results.Text(json, "application/json");
        });

        Console.WriteLine($"Serving {siteDir} with replay for {string.Join(", ", datasets.Select(x => x.Id))} on http://localhost:{port}");
        await app.RunAsync();
    }

    /// <summary>
    /// One batch: the timeline for candles start to start + BatchSize, plus the candle before so items already holding at the
    /// start are known to be carried over rather than new.
    /// </summary>
    private static string Batch(Dataset data, int start)
    {
        var to = Math.Min(start + BatchSize, data.Prices.Count);
        var timeline = MarketStructure.Timeline(data.Prices, EnumPriceBasis.Close, MaxLevel, from: Math.Max(0, start - 1), to: to,
            keepFrom: Math.Max(0, start - Window));
        return JsonSerializer.Serialize(new { From = start, To = to, Timeline = timeline }, Json);
    }
}
