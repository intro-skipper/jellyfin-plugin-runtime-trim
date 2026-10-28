using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RuntimeTrim;

/// <summary>
/// Registers <see cref="Entrypoint"/> and the <see cref="TrimStore"/> it shares with
/// <see cref="TrimmedController"/>.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton(services => new TrimStore(
            Path.Join(services.GetRequiredService<IApplicationPaths>().DataPath, "runtime-trim.json"),
            services.GetRequiredService<ILibraryManager>().GetItemById,
            services.GetRequiredService<ILogger<TrimStore>>()));
        serviceCollection.AddHostedService<Entrypoint>();
    }
}
