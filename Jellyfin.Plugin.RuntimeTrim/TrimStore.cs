using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RuntimeTrim;

/// <summary>
/// The runtimes the plugin replaced, which Jellyfin does not keep. <see cref="Entrypoint"/>
/// records each trim and <see cref="TrimmedController"/> serves them to the settings page.
/// </summary>
/// <remarks>
/// A re-probe resets the runtime without telling the store, so a reader checks that an entry
/// still matches the item's runtime. Entries are dropped when their item is removed. The file
/// is a convenience: failing to read or write it is logged and never thrown.
/// </remarks>
/// <param name="path">The JSON file the trims are kept in. A missing or unreadable file starts empty.</param>
/// <param name="logger">Reports a file that cannot be read or written.</param>
public sealed partial class TrimStore(string path, ILogger<TrimStore> logger)
{
    private readonly ConcurrentDictionary<Guid, Trim> _trims = Load(path, logger);
    private readonly Lock _writeLock = new();

    /// <summary>
    /// Gets the recorded trims by item id. Safe to enumerate while trims are recorded.
    /// </summary>
    public IEnumerable<KeyValuePair<Guid, Trim>> Trims => _trims;

    /// <summary>
    /// Records or replaces the trim of an item and saves the file.
    /// </summary>
    /// <param name="itemId">The trimmed item.</param>
    /// <param name="trim">Its runtime before and after the trim.</param>
    public void Set(Guid itemId, Trim trim)
    {
        lock (_writeLock)
        {
            _trims[itemId] = trim;
            Save();
        }
    }

    /// <summary>
    /// Forgets the trim of an item, saving the file only when there was one.
    /// </summary>
    /// <param name="itemId">The item.</param>
    public void Remove(Guid itemId)
    {
        lock (_writeLock)
        {
            if (_trims.TryRemove(itemId, out _))
            {
                Save();
            }
        }
    }

    // Written beside the file and moved over it, so a crash mid-write cannot truncate it. A failed
    // save keeps the trims in memory, and the next save writes them all.
    private void Save()
    {
        try
        {
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_trims));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(logger, ex, path);
        }
    }

    private static ConcurrentDictionary<Guid, Trim> Load(string path, ILogger logger)
    {
        try
        {
            return JsonSerializer.Deserialize<ConcurrentDictionary<Guid, Trim>>(File.ReadAllText(path)) ?? new();
        }
        catch (FileNotFoundException)
        {
            return new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LogLoadFailed(logger, ex, path);
            return new();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not save the list of trimmed videos to {Path}")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read the list of trimmed videos from {Path}, so it starts empty")]
    private static partial void LogLoadFailed(ILogger logger, Exception exception, string path);
}

/// <summary>
/// A video's runtime as Jellyfin probed it from the container, and where its video stream ends.
/// </summary>
/// <param name="FileTicks">The runtime before the trim.</param>
/// <param name="VideoTicks">The runtime after the trim.</param>
public readonly record struct Trim(long FileTicks, long VideoTicks);
