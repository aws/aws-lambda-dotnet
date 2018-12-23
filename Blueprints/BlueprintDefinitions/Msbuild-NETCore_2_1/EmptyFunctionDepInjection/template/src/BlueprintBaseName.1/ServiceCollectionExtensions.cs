using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BlueprintBaseName._1
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddConfiguration(
            this IServiceCollection services,
            Func<IConfigurationBuilder, IConfiguration> builderFunc)
        {
            if (builderFunc == null)
                throw new ArgumentNullException(nameof(builderFunc));
            return services.AddTransient(provider => builderFunc(new ConfigurationBuilder()));
        }
    }
}