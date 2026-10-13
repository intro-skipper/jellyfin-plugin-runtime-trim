using Xunit;

namespace Jellyfin.Plugin.RuntimeTrim.Tests;

public class WebInjectorTests
{
    [Fact]
    public void TransformAddsScriptBeforeBodyEnd()
    {
        var html = WebInjector.Transform(new PayloadRequest("<html><body><main></main></body></html>"));

        Assert.StartsWith("<html><body><main></main><script>", html, StringComparison.Ordinal);
        Assert.Contains("addEventListener('timeupdate'", html, StringComparison.Ordinal);
        Assert.EndsWith("</script></body></html>", html, StringComparison.Ordinal);
    }
}
