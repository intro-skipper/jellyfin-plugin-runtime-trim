using System.Threading.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RuntimeTrim;

/// <summary>
/// Shortens a video's runtime to where its video stream ends when other streams run past it,
/// such as a dub padded with trailing silence, and registers the web script that ends
/// direct play at that runtime. Each trim is recorded in <see cref="TrimStore"/>.
/// </summary>
/// <remarks>
/// Jellyfin's probe sets the runtime from the whole container on every re-probe, including
/// re-probes caused by external subtitle changes, so the trim is re-applied after each
/// metadata import rather than once from a metadata provider.
/// </remarks>
internal sealed partial class Entrypoint(
    ILibraryManager libraryManager,
    IMediaEncoder mediaEncoder,
    TrimStore trims,
    ILogger<Entrypoint> logger) : BackgroundService
{
    // Probed one at a time, so a large first scan does not pile up ffprobe processes.
    private readonly Channel<Video> _imported = Channel.CreateUnbounded<Video>();

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // File Transformation is optional and reached by reflection. Whatever it throws must not
        // escape: it would skip the trim, and a faulted BackgroundService stops the server.
        try
        {
            if (!WebInjector.Register())
            {
                LogNoFileTransformation(logger);
            }
        }
        catch (Exception ex)
        {
            LogRegisterFailed(logger, ex);
        }

        libraryManager.ItemUpdated += OnItemUpdated;
        libraryManager.ItemRemoved += OnItemRemoved;
        try
        {
            await foreach (var video in _imported.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                await TrimAsync(video, stoppingToken).ConfigureAwait(false);
            }
        }
        finally
        {
            libraryManager.ItemUpdated -= OnItemUpdated;
            libraryManager.ItemRemoved -= OnItemRemoved;
        }
    }

    private void OnItemUpdated(object? sender, ItemChangeEventArgs e)
    {
        // The probe saves with MetadataImport. The trim saves with None, so it does not
        // retrigger itself.
        if (e.UpdateReason.HasFlag(ItemUpdateType.MetadataImport)
            && e.Item is Video { VideoType: VideoType.VideoFile, IsFileProtocol: true, IsShortcut: false, RunTimeTicks: > 0 } video)
        {
            _imported.Writer.TryWrite(video);
        }
    }

    private void OnItemRemoved(object? sender, ItemChangeEventArgs e) => trims.Remove(e.Item.Id);

    private async Task TrimAsync(Video video, CancellationToken cancellationToken)
    {
        try
        {
            // An excluded library keeps the runtime the probe just set.
            if (Plugin.Instance?.Configuration.ExcludedLibraryIds is { Length: > 0 } excluded
                && libraryManager.GetCollectionFolders(video).Any(library => excluded.Contains(library.Id)))
            {
                return;
            }

            var runtime = TimeSpan.FromTicks(video.RunTimeTicks ?? 0);
            if (await Trim.ProbeAsync(mediaEncoder.ProbePath, video.Path, runtime, cancellationToken).ConfigureAwait(false) is not { } trim)
            {
                return;
            }

            video.RunTimeTicks = trim.VideoTicks;

            // None rather than MetadataEdit: an edit also rewrites NFO files and forces every image
            // to be re-processed.
            await video.UpdateToRepositoryAsync(ItemUpdateType.None, cancellationToken).ConfigureAwait(false);
            LogTrimmed(logger, video.Path, runtime, TimeSpan.FromTicks(trim.VideoTicks));
            trims.Set(video.Id, trim);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Server shutdown.
        }
        catch (Exception ex)
        {
            LogTrimFailed(logger, ex, video.Path);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Trimmed runtime of {Path} from {Runtime} to where its video ends, {VideoEnd}")]
    private static partial void LogTrimmed(ILogger logger, string path, TimeSpan runtime, TimeSpan videoEnd);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not trim runtime of {Path}")]
    private static partial void LogTrimFailed(ILogger logger, Exception exception, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "File Transformation is not installed, so direct play in the web client still runs to the end of the file")]
    private static partial void LogNoFileTransformation(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not register the web script with File Transformation, so direct play in the web client still runs to the end of the file")]
    private static partial void LogRegisterFailed(ILogger logger, Exception exception);
}
