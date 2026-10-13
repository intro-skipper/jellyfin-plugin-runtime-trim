using Xunit;

namespace Jellyfin.Plugin.RuntimeTrim.Tests;

public class ConfigPageTests
{
    // jellyfin-web replaces every "${key}" in a plugin page with a translation before the page's
    // script runs, so a template literal in the script would render its own source text.
    [Fact]
    public void PageHasNoTemplateInterpolation()
    {
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("Jellyfin.Plugin.RuntimeTrim.configPage.html");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);

        Assert.DoesNotContain("${", reader.ReadToEnd(), StringComparison.Ordinal);
    }
}
