using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BlueprintBaseName._1
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add configuration to an IServiceCollection.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="builderFunc"></param>
        /// <returns></returns>
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