using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.RuntimeTrim.Tests;

public sealed class TrimStoreTests : IDisposable
{
    private readonly string _path = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void TrimsSurviveReload()
    {
        var kept = Guid.NewGuid();
        var removed = Guid.NewGuid();
        var store = new TrimStore(_path, NullLogger<TrimStore>.Instance);
        store.Set(kept, new Trim(100, 50));
        store.Set(removed, new Trim(200, 150));
        store.Remove(removed);

        Assert.Equal(new KeyValuePair<Guid, Trim>(kept, new Trim(100, 50)), Assert.Single(new TrimStore(_path, NullLogger<TrimStore>.Instance).Trims));
    }

    // The store is built when the server starts its services, so a bad file must not throw.
    [Fact]
    public void UnreadableFileStartsEmpty()
    {
        File.WriteAllText(_path, "{\"truncated");

        Assert.Empty(new TrimStore(_path, NullLogger<TrimStore>.Instance).Trims);
    }
}
