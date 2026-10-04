using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Gradient.CryptoAnalysis.Site;

/// <summary>
/// Where the making of the tour's audio or video has got to, and the video there is: when it was made, and whether the tour
/// has been saved since. AudioOnly says the run under way, or the last one, read the texts aloud without recording.
/// </summary>
public sealed record TourVideoStatus(bool Running, bool AudioOnly, string Stage, int Seconds, string? Error, DateTime? Made, long Size, bool Stale);

/// <summary>
/// Makes the tour's audio and video for the page's Update audio and Generate video buttons by running tools/tour-video: the
/// texts are read aloud, and for the video the tour is then played in a headless browser against this server and recorded.
/// The reading is done by a worker kept running between runs, so the model loads once; a tour written from the prices can
/// have its texts read ahead the same way. One run at a time.
/// </summary>
public sealed class TourVideo(string toolDir, string videoPath, string tourPath, string siteUrl) : IDisposable
{
    private const int LinesKept = 12;
    private readonly object gate = new();
    private readonly SemaphoreSlim reading = new(1, 1);
    private readonly List<string> lines = [];
    private Process? worker;
    private string? pending;
    private bool running;
    private bool audioOnly;
    private string stage = "";
    private string? error;
    private DateTime started;

    public string VideoPath => videoPath;

    /// <summary>
    /// The tour as the page plays it, with every section that explains something written out, as JSON: what the texts are
    /// read from. Set by the server, which has the datasets to write them with.
    /// </summary>
    public Func<string?>? TourSource { get; set; }

    /// <summary>
    /// Starts reading the texts aloud and, unless only the audio is wanted, making the video. Does nothing while a run is
    /// under way.
    /// </summary>
    public void Start(bool onlyAudio = false)
    {
        if (!Begin(onlyAudio))
            return;
        _ = Task.Run(Make);
    }

    /// <summary>
    /// Reads the texts of a tour aloud in the background, so its clips are there by the time its Voice button is pressed:
    /// those with no clip yet, or whose clip was read from other words. While a run is under way the tour waits its turn,
    /// the latest asked for taking the place of any waiting before it.
    /// </summary>
    public void Narrate(string tourJson)
    {
        lock (gate)
        {
            if (running)
            {
                pending = tourJson;
                return;
            }
        }

        if (!Begin(onlyAudio: true))
            return;
        _ = Task.Run(async () =>
        {
            string? failure = null;
            try
            {
                if (await Read(tourJson) is { } problem)
                    failure = problem;
            }
            catch (Exception e)
            {
                failure = e.Message;
            }

            End(failure);
        });
    }

    /// <summary>
    /// How the making is going, and the video there is.
    /// </summary>
    public TourVideoStatus Status()
    {
        var file = new FileInfo(videoPath);
        lock (gate)
        {
            // A video being written over is not there to download yet.
            DateTime? made = file.Exists && (!running || audioOnly) ? file.LastWriteTimeUtc : null;
            return new TourVideoStatus(running, audioOnly, stage, running ? (int)(DateTime.UtcNow - started).TotalSeconds : 0, error, made,
                made != null ? file.Length : 0, made != null && File.GetLastWriteTimeUtc(tourPath) > made);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (gate)
        {
            try
            {
                if (worker is { HasExited: false })
                    worker.Kill(entireProcessTree: true);
            }
            catch (Exception)
            {
                // The worker is going anyway.
            }

            worker?.Dispose();
            worker = null;
        }
    }

    private bool Begin(bool onlyAudio)
    {
        lock (gate)
        {
            if (running)
                return false;
            running = true;
            audioOnly = onlyAudio;
            error = null;
            stage = "Starting";
            started = DateTime.UtcNow;
            lines.Clear();
            return true;
        }
    }

    private void End(string? failure)
    {
        string? next;
        lock (gate)
        {
            running = false;
            error = failure;
            next = pending;
            pending = null;
        }

        if (next != null)
            Narrate(next);
    }

    private async Task Make()
    {
        string? failure = null;
        try
        {
            if (!Directory.Exists(Path.Combine(toolDir, "node_modules")))
                failure = "tools/tour-video is not set up: run npm install there, then npx playwright install chromium";
            else if (TourSource?.Invoke() is not { } tour)
                failure = "the tour could not be read";
            else if (await Read(tour) is { } problem)
                failure = problem;
            else if (!audioOnly && await Node("Recording the tour", "record.mjs", "--url", siteUrl, "--out", videoPath) != 0)
                failure = Failure();
        }
        catch (Exception e)
        {
            failure = $"node could not be run: {e.Message}";
        }

        End(failure);
    }

    // Has the worker read a tour's texts; null when it did, else what went wrong.
    private async Task<string?> Read(string tourJson)
    {
        lock (gate)
            stage = "Reading the texts aloud";

        await reading.WaitAsync();
        try
        {
            var process = Worker();
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { tour = JsonNode.Parse(tourJson) }));
            await process.StandardInput.FlushAsync();
            while (await process.StandardOutput.ReadLineAsync() is { } line)
            {
                JsonObject? reply;
                try
                {
                    reply = JsonNode.Parse(line) as JsonObject;
                }
                catch (JsonException)
                {
                    Said(line);
                    continue;
                }

                if (reply?["done"] != null)
                {
                    Said($"{reply["clips"]} clips, {reply["read"]} sentences read");
                    return null;
                }

                if (reply?["error"]?.GetValue<string>() is { } problem)
                    return problem;
                if (reply?["read"] is JsonValue progress && progress.TryGetValue<string>(out var read))
                    Said(read);
            }

            return Failure("the narration worker stopped");
        }
        finally
        {
            reading.Release();
        }
    }

    // The narration worker, started on first use and again if it has gone.
    private Process Worker()
    {
        lock (gate)
        {
            if (worker is { HasExited: false })
                return worker;
            worker?.Dispose();
            var info = new ProcessStartInfo("node")
            {
                WorkingDirectory = toolDir,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            info.ArgumentList.Add("worker.mjs");
            worker = Process.Start(info) ?? throw new InvalidOperationException("the narration worker did not start");
            worker.ErrorDataReceived += (_, e) => Said(e.Data);
            worker.BeginErrorReadLine();
            return worker;
        }
    }

    private async Task<int> Node(string doing, string script, params string[] arguments)
    {
        lock (gate)
            stage = doing;

        var info = new ProcessStartInfo("node")
        {
            WorkingDirectory = toolDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add(script);
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        using var process = Process.Start(info) ?? throw new InvalidOperationException("it did not start");
        process.OutputDataReceived += (_, e) => Said(e.Data);
        process.ErrorDataReceived += (_, e) => Said(e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private void Said(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        lock (gate)
        {
            lines.Add(line.Trim());
            if (lines.Count > LinesKept)
                lines.RemoveAt(0);
            // The recording is done once the tool says so; what is left is laying the clips over it.
            if (line.StartsWith("Recorded "))
                stage = "Adding the narration";
        }
    }

    private string Failure(string? fallback = null)
    {
        // The line naming an error when there is one, else the last thing the tool said.
        lock (gate)
            return lines.FirstOrDefault(x => x.Contains("Error", StringComparison.OrdinalIgnoreCase)) ?? lines.LastOrDefault() ?? fallback ?? "the video tool failed";
    }
}
