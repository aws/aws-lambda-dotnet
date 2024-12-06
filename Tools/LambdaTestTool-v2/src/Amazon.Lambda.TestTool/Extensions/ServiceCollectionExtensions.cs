using Amazon.Lambda.TestTool.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Amazon.Lambda.TestTool.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddCustomServices(this IServiceCollection serviceCollection,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        serviceCollection.TryAdd(new ServiceDescriptor(typeof(ICommandFactory), typeof(CommandFactory), lifetime));
        serviceCollection.TryAdd(new ServiceDescriptor(typeof(IToolInteractiveService), typeof(ConsoleInteractiveService), lifetime));
        
        serviceCollection.AddSingleton<AppRunner>();
    }
}