using System.Runtime.Loader;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.RuntimeTrim;

/// <summary>
/// Inlines <c>runtimeTrim.js</c> into jellyfin-web's index.html through the File
/// Transformation plugin. Without that plugin, only the server-side runtime fix applies.
/// </summary>
internal static class WebInjector
{
    private static readonly Lazy<string> ScriptTag = new(() =>
    {
        using var stream = typeof(WebInjector).Assembly.GetManifestResourceStream("Jellyfin.Plugin.RuntimeTrim.runtimeTrim.js")
            ?? throw new InvalidOperationException("runtimeTrim.js is not embedded.");
        using var reader = new StreamReader(stream);
        return $"<script>{reader.ReadToEnd()}</script>";
    });

    /// <summary>
    /// Gets a value indicating whether <see cref="Register"/> succeeded, so the script ends
    /// direct play. The settings page warns when it did not.
    /// </summary>
    internal static bool Registered { get; private set; }

    /// <summary>
    /// Registers <see cref="Transform"/> with File Transformation. It lives in another plugin
    /// load context, so it is reached by reflection.
    /// </summary>
    /// <returns><see langword="false"/> when File Transformation is not installed.</returns>
    internal static bool Register()
    {
        var pluginInterface = AssemblyLoadContext.All
            .SelectMany(context => context.Assemblies)
            .FirstOrDefault(assembly => assembly.FullName?.Contains(".FileTransformation", StringComparison.Ordinal) ?? false)
            ?.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");

        if (pluginInterface?.GetMethod("RegisterTransformation") is not { } register)
        {
            return false;
        }

        register.Invoke(null, [new JObject
        {
            { "id", Plugin.PluginId },
            { "fileNamePattern", "index.html" },
            { "callbackAssembly", typeof(WebInjector).Assembly.FullName },
            { "callbackClass", typeof(WebInjector).FullName },
            { "callbackMethod", nameof(Transform) },
        }]);
        Registered = true;
        return true;
    }

    /// <summary>
    /// File Transformation callback for index.html. File Transformation finds it by name, so it
    /// must stay public.
    /// </summary>
    /// <param name="payload">The page as File Transformation read it.</param>
    /// <returns>The page with the script added before its last <c>&lt;/body&gt;</c>, or unchanged when it has none.</returns>
    public static string Transform(PayloadRequest payload)
    {
        var contents = payload.Contents ?? string.Empty;
        var end = contents.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return end < 0 ? contents : contents.Insert(end, ScriptTag.Value);
    }
}

/// <summary>
/// The file File Transformation passes to <see cref="WebInjector.Transform"/>. It binds the
/// JSON <c>contents</c> field with Newtonsoft, which matches names case-insensitively.
/// </summary>
/// <param name="Contents">The file's current contents.</param>
internal sealed record PayloadRequest(string? Contents);
