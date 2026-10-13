using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.RuntimeTrim;

/// <summary>
/// Settings edited on the plugin's dashboard page, <c>configPage.html</c>.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the libraries whose videos keep their full runtime. Libraries are excluded
    /// rather than selected so that a new library is trimmed without visiting the page.
    /// </summary>
    public Guid[] ExcludedLibraryIds { get; set; } = [];
}
