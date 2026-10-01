using System.Diagnostics;

namespace Gradient.CryptoAnalysis.Site;

/// <summary>
/// Where the making of the tour's audio or video has got to, and the video there is: when it was made, and whether the tour
/// has been saved since. AudioOnly says the run under way, or the last one, read the texts aloud without recording.
/// </summary>
public sealed record TourVideoStatus(bool Running, bool AudioOnly, string Stage, int Seconds, string? Error, DateTime? Made, long Size, bool Stale);

/// <summary>
/// Makes the tour's audio and video for the page's Update audio and Generate video buttons by running tools/tour-video: the
/// texts are read aloud, and for the video the tour is then played in a headless browser against this server and recorded.
/// One run at a time.
/// </summary>
public sealed class TourVideo(string toolDir, string videoPath, string tourPath, string siteUrl)
{
    private const int LinesKept = 12;
    private readonly object gate = new();
    private readonly List<string> lines = [];
    private bool running;
    private bool audioOnly;
    private string stage = "";
    private string? error;
    private DateTime started;

    public string VideoPath => videoPath;

    /// <summary>
    /// Starts reading the texts aloud and, unless only the audio is wanted, making the video. Does nothing while a run is
    /// under way.
    /// </summary>
    public void Start(bool onlyAudio = false)
    {
        lock (gate)
        {
            if (running)
                return;
            running = true;
            audioOnly = onlyAudio;
            error = null;
            stage = "Starting";
            started = DateTime.UtcNow;
            lines.Clear();
        }

        _ = Task.Run(Make);
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

    private async Task Make()
    {
        string? failure = null;
        try
        {
            if (!Directory.Exists(Path.Combine(toolDir, "node_modules")))
                failure = "tools/tour-video is not set up: run npm install there, then npx playwright install chromium";
            else if (await Node("Reading the texts aloud", "narrate.mjs") != 0
                || (!audioOnly && await Node("Recording the tour", "record.mjs", "--url", siteUrl, "--out", videoPath) != 0))
                failure = Failure();
        }
        catch (Exception e)
        {
            failure = $"node could not be run: {e.Message}";
        }

        lock (gate)
        {
            running = false;
            error = failure;
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

    private string Failure()
    {
        // The line naming an error when there is one, else the last thing the tool said.
        lock (gate)
            return lines.FirstOrDefault(x => x.Contains("Error", StringComparison.OrdinalIgnoreCase)) ?? lines.LastOrDefault() ?? "the video tool failed";
    }
}
