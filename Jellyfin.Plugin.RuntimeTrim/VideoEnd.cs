using System.Diagnostics;
using System.Globalization;

namespace Jellyfin.Plugin.RuntimeTrim;

/// <summary>
/// Reads where a file's first video stream ends with ffprobe.
/// </summary>
internal static class VideoEnd
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Runs ffprobe on <paramref name="path"/> and reads its first video stream.
    /// </summary>
    /// <returns>
    /// Where the video stream ends, measured from the container's start like the runtime, or
    /// <see langword="null"/> when the file has no video stream or reports no per-stream length.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled or ffprobe ran longer than 30 seconds.
    /// ffprobe is killed first.
    /// </exception>
    /// <exception cref="InvalidOperationException">ffprobe did not start.</exception>
    public static async Task<TimeSpan?> ProbeAsync(string ffprobePath, string path, CancellationToken cancellationToken)
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
            return Parse(output);
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
    /// The video's start plus its length, less the container's start, or <see langword="null"/>
    /// when the output has no video length.
    /// </returns>
    internal static TimeSpan? Parse(string output)
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

        return videoStart - containerStart + (length ?? tagged);
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
