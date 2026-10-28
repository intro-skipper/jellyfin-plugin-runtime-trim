using System.Text.Json;
using MediaBrowser.Controller.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RuntimeTrim;

/// <summary>
/// The runtimes the plugin replaced, which Jellyfin does not keep. <see cref="Entrypoint"/>
/// records each trim and <see cref="TrimmedController"/> lists the ones still in effect.
/// </summary>
/// <remarks>
/// A re-probe resets the runtime without telling the store, so <see cref="GetCurrent"/> leaves
/// out an entry whose item's runtime no longer matches. It keeps the entry rather than forgetting
/// it, because Jellyfin's item cache can briefly hold an untrimmed copy, so the entry stays left
/// out until a later trim replaces it or the item is removed. The file is a convenience: failing
/// to read or write it is logged and never thrown.
/// </remarks>
/// <param name="path">The JSON file the trims are kept in. A missing or unreadable file starts empty.</param>
/// <param name="getItem">Looks up an item by id, returning <see langword="null"/> once it is removed.</param>
/// <param name="logger">Reports a file that cannot be read or written.</param>
public sealed partial class TrimStore(string path, Func<Guid, BaseItem?> getItem, ILogger<TrimStore> logger)
{
    private readonly Dictionary<Guid, Trim> _trims = Load(path, logger);
    private readonly Lock _lock = new();

    /// <summary>
    /// Records or replaces the trim of an item and saves the file.
    /// </summary>
    /// <param name="itemId">The trimmed item.</param>
    /// <param name="trim">Its runtime before and after the trim.</param>
    public void Set(Guid itemId, Trim trim)
    {
        lock (_lock)
        {
            _trims[itemId] = trim;
            Save();
        }
    }

    /// <summary>
    /// Gets the videos whose runtime is still trimmed, and forgets the trims of removed items,
    /// saving the file when there were any.
    /// </summary>
    /// <returns>The videos with their trims, unordered.</returns>
    public IReadOnlyList<(Video Video, Trim Trim)> GetCurrent()
    {
        var current = new List<(Video, Trim)>();
        var removed = new List<Guid>();
        lock (_lock)
        {
            foreach (var (id, trim) in _trims)
            {
                switch (getItem(id))
                {
                    case null:
                        removed.Add(id);
                        break;
                    case Video video when video.RunTimeTicks == trim.VideoTicks:
                        current.Add((video, trim));
                        break;
                }
            }

            if (removed.Count > 0)
            {
                removed.ForEach(id => _trims.Remove(id));
                Save();
            }
        }

        return current;
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

    private static Dictionary<Guid, Trim> Load(string path, ILogger logger)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<Guid, Trim>>(File.ReadAllText(path)) ?? new();
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
