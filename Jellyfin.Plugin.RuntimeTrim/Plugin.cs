using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.RuntimeTrim;

/// <summary>
/// Plugin identity, settings and settings page. The work happens in <see cref="Entrypoint"/>.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// The plugin's id, also used as its File Transformation registration id.
    /// </summary>
    internal static readonly Guid PluginId = new("997a526b-ac90-4198-8353-450a9f74f4d5");

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class. The server creates it once,
    /// before any service starts.
    /// </summary>
    /// <param name="applicationPaths">The server's paths.</param>
    /// <param name="xmlSerializer">Reads and writes the configuration file.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the loaded plugin. The server does not register plugins as services, so this is how
    /// services read <see cref="BasePlugin{T}.Configuration"/>.
    /// </summary>
    internal static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Runtime Trim";

    /// <inheritdoc />
    public override string Description => "Ends playback when the video ends, even if an audio track runs longer.";

    /// <inheritdoc />
    public override Guid Id => PluginId;

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages() =>
    [
        new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = $"{GetType().Namespace}.configPage.html",
        },
    ];
}
