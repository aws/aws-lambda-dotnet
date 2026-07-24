using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace TestWebApp
{
    /// <summary>
    /// Test-only helper for turning off change tracking on file-based configuration sources (for example
    /// the appsettings.json and appsettings.{Environment}.json files added by Host.CreateDefaultBuilder).
    /// <para>
    /// The tests never care about reloading configuration in response to a file change. On Linux each
    /// watched file consumes an inotify instance, and the per-user inotify limit is easily exhausted when
    /// the tests create many ASP.NET Core hosts, producing "The configured user limit on the number of
    /// inotify instances has been reached". Disabling the watchers avoids consuming inotify instances.
    /// </para>
    /// <para>
    /// This lives in test support code only; the shipping Amazon.Lambda.AspNetCoreServer package is
    /// unchanged. Lambda function classes used by the tests call this from their Init(IHostBuilder) override.
    /// </para>
    /// </summary>
    public static class DisableConfigFileWatching
    {
        public static void Apply(IHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                foreach (var source in config.Sources.OfType<FileConfigurationSource>())
                {
                    source.ReloadOnChange = false;
                }
            });
        }
    }
}
