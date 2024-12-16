using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Amazon.Lambda.TestTool.Services;

/// <summary>
/// Configures static file options for the application by setting up a composite file provider 
/// that includes embedded resources from the assembly and the existing web root file provider.
/// </summary>
internal class ConfigureStaticFilesOptions(IWebHostEnvironment environment)
    : IPostConfigureOptions<StaticFileOptions>
{
    /// <summary>
    /// Configures the <see cref="StaticFileOptions"/> for the application.
    /// </summary>
    /// <param name="name">The name of the options instance being configured.</param>
    /// <param name="options">The options instance to configure.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="options"/> is <c>null</c>.</exception>
    public void PostConfigure(string? name, StaticFileOptions options)
    {
        name = name ?? throw new ArgumentNullException(nameof(name));
        options = options ?? throw new ArgumentNullException(nameof(options));

        if (name != Options.DefaultName)
        {
            return;
        }

        var fileProvider = new ManifestEmbeddedFileProvider(typeof(Program).Assembly, "wwwroot");
        environment.WebRootFileProvider = new CompositeFileProvider(fileProvider, environment.WebRootFileProvider);
    }
}