using System.Diagnostics;
using System.Globalization;

namespace Jellyfin.Plugin.RuntimeTrim;

/// <summary>
/// A video's runtime as Jellyfin probed it from the container, and where its video stream ends.
/// <see cref="ProbeAsync"/> measures it with ffprobe and decides whether the gap is padding.
/// </summary>
/// <param name="FileTicks">The runtime before the trim.</param>
/// <param name="VideoTicks">The runtime after the trim.</param>
public readonly record struct Trim(long FileTicks, long VideoTicks)
{
    // Containers often end a fraction of a second after the video. Only trim real padding.
    private static readonly TimeSpan MinimumTrim = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Runs ffprobe on <paramref name="path"/> and reads where its first video stream ends.
    /// </summary>
    /// <param name="ffprobePath">The server's ffprobe.</param>
    /// <param name="path">The video file.</param>
    /// <param name="runtime">The runtime Jellyfin probed from the whole container.</param>
    /// <param name="cancellationToken">Stops ffprobe.</param>
    /// <returns>
    /// The trim, or <see langword="null"/> when the runtime should stay: see <see cref="Parse"/>.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled or ffprobe ran longer than 30 seconds.
    /// ffprobe is killed first.
    /// </exception>
    /// <exception cref="InvalidOperationException">ffprobe did not start.</exception>
    internal static async Task<Trim?> ProbeAsync(string ffprobePath, string path, TimeSpan runtime, CancellationToken cancellationToken)
    {
        // "V" skips attached pictures such as MP4 cover art.
        string[] args = ["-v", "error", "-select_streams", "V:0", "-show_entries", "stream=start_time,duration:stream_tags=DURATION,DURATION-eng:format=start_time", path];
        var startInfo = new ProcessStartInfo(ffprobePath, args)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("ffprobe did not start.");
        try
        {
            var output = await process.StandardOutput.ReadToEndAsync(timeout.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return Parse(output, runtime);
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            throw;
        }
    }

    /// <summary>
    /// Parses ffprobe's <c>default</c> output: the video's <c>[STREAM]</c> section, which MPEG-TS
    /// also repeats inside a <c>[PROGRAM]</c> section, then the container's <c>[FORMAT]</c>
    /// section. MP4 reports the length as <c>duration=</c> in seconds. Matroska reports
    /// <c>duration=N/A</c> and stores it in a <c>DURATION</c> or <c>DURATION-eng</c> tag. ffmpeg's
    /// own Matroska muxer writes the end time there instead, so a late-starting video trims less.
    /// </summary>
    /// <returns>
    /// The trim to where the video ends, its start plus its length less the container's start, or
    /// <see langword="null"/> when the output has no video length, the file runs less than 5 seconds
    /// past the video, or the video ends before half the runtime.
    /// </returns>
    internal static Trim? Parse(string output, TimeSpan runtime)
    {
        // The runtime counts from the container's start, which MPEG-TS puts well past zero.
        var videoStart = TimeSpan.Zero;
        var containerStart = TimeSpan.Zero;
        var inFormat = false;
        TimeSpan? length = null;
        TimeSpan? tagged = null;
        foreach (var line in output.Split('\n', StringSplitOptions.TrimEntries))
        {
            switch (line.Split('=', 2))
            {
                case ["[FORMAT]"]:
                    inFormat = true;
                    break;
                case ["start_time", var value] when inFormat && double.TryParse(value, CultureInfo.InvariantCulture, out var seconds):
                    containerStart = TimeSpan.FromSeconds(seconds);
                    break;
                case ["start_time", var value] when double.TryParse(value, CultureInfo.InvariantCulture, out var seconds):
                    videoStart = TimeSpan.FromSeconds(seconds);
                    break;
                case ["duration", var value] when double.TryParse(value, CultureInfo.InvariantCulture, out var seconds):
                    length = TimeSpan.FromSeconds(seconds);
                    break;
                case ["TAG:DURATION" or "TAG:DURATION-eng", var value]:
                    tagged ??= ParseTag(value);
                    break;
            }
        }

        // A video ending before half the file is a still image or a broken track, not padding.
        return videoStart - containerStart + (length ?? tagged) is { } end && runtime - end >= MinimumTrim && end >= runtime / 2
            ? new Trim(runtime.Ticks, end.Ticks)
            : null;
    }

    // "00:23:40.087000000". TimeSpan.Parse rejects more than 7 fractional digits.
    private static TimeSpan? ParseTag(string value)
        => value.Split(':') is [var h, var m, var s]
            && int.TryParse(h, CultureInfo.InvariantCulture, out var hours)
            && int.TryParse(m, CultureInfo.InvariantCulture, out var minutes)
            && double.TryParse(s, CultureInfo.InvariantCulture, out var seconds)
            ? TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds)
            : null;
}
