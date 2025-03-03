using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TestHostBuilderApp;

[Amazon.Lambda.Annotations.LambdaStartup]
public class Startup
{
    public HostApplicationBuilder ConfigureHostBuilder()
    {
        var builder = new HostApplicationBuilder();
        builder.Services.AddSingleton<ICalculatorService>(new CalculatorService());
        return builder;
    }
}
