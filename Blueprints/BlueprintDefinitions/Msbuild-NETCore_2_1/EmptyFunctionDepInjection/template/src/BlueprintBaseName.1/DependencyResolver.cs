using System;
using Microsoft.Extensions.DependencyInjection;

namespace BlueprintBaseName._1
{
    public class DependencyResolver
    {
        public IServiceProvider ServiceProvider { get; }

        public DependencyResolver(Action<IServiceCollection> registerServices)
        {
            var services = new ServiceCollection();
            registerServices?.Invoke(services);
            ServiceProvider = services.BuildServiceProvider();
        }
    }
}