using MediaBrowser.Controller.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.RuntimeTrim.Tests;

public sealed class TrimStoreTests : IDisposable
{
    private readonly string _path = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly Dictionary<Guid, BaseItem> _items = [];

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void RemovedItemIsForgottenInTheFile()
    {
        var kept = new Video { Id = Guid.NewGuid(), RunTimeTicks = 50 };
        var removed = Guid.NewGuid();
        _items[kept.Id] = kept;
        var store = Open();
        store.Set(kept.Id, new Trim(100, 50));
        store.Set(removed, new Trim(200, 150));

        Assert.Equal((kept, new Trim(100, 50)), Assert.Single(store.GetCurrent()));
        Assert.DoesNotContain(removed.ToString(), File.ReadAllText(_path), StringComparison.Ordinal);
        Assert.Equal((kept, new Trim(100, 50)), Assert.Single(Open().GetCurrent()));
    }

    // The probe set the runtime back to the whole file, and the next trim restores it.
    [Fact]
    public void ReprobedItemIsHiddenUntilTrimmedAgain()
    {
        var video = new Video { Id = Guid.NewGuid(), RunTimeTicks = 100 };
        _items[video.Id] = video;
        var store = Open();
        store.Set(video.Id, new Trim(100, 50));

        Assert.Empty(store.GetCurrent());
        video.RunTimeTicks = 50;
        Assert.Equal((video, new Trim(100, 50)), Assert.Single(store.GetCurrent()));
    }

    // The store is built when the server starts its services, so a bad file must not throw.
    [Fact]
    public void UnreadableFileDoesNotThrow()
    {
        File.WriteAllText(_path, "{\"truncated");

        Assert.Null(Record.Exception(Open));
    }

    private TrimStore Open() => new(_path, id => _items.GetValueOrDefault(id), NullLogger<TrimStore>.Instance);
}
