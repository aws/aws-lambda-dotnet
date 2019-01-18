using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetServerless.Lambda
{
  public class Startup
  {
    public static IServiceCollection BuildContainer()
    {
      var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddEnvironmentVariables()
        .Build();

      return ConfigureServices(configuration);
    }


    private static IServiceCollection ConfigureServices(IConfigurationRoot configurationRoot)
    {
      var services = new ServiceCollection();

      return services;
    }
  }
}
