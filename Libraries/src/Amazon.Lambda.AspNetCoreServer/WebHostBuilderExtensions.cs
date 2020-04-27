using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Amazon.Lambda.AspNetCoreServer.Internal;
using Microsoft.AspNetCore.Hosting.Server;

namespace Microsoft.AspNetCore.Hosting
{
    /// <summary>
    /// This class is a container for extensions methods to the IWebHostBuilder
    /// </summary>
    public static class WebHostBuilderExtensions
    {
        /// <summary>
        /// Extension method for configuring API Gateway as the server for an ASP.NET Core application.
        /// This is called instead of UseKestrel. If UseKestrel was called before this it will remove
        /// the service description that was added to the IServiceCollection.
        /// </summary>
        /// <param name="hostBuilder"></param>
        /// <returns></returns>
        [Obsolete("Calls should be replaced with UseLambdaServer")]
        public static IWebHostBuilder UseApiGateway(this IWebHostBuilder hostBuilder)
        {
            return UseLambdaServer(hostBuilder);
        }

        /// <summary>
        /// Extension method for configuring Lambda as the server for an ASP.NET Core application.
        /// This is called instead of UseKestrel. If UseKestrel was called before this it will remove
        /// the service description that was added to the IServiceCollection.
        /// </summary>
        /// <param name="hostBuilder"></param>
        /// <returns></returns>
        public static IWebHostBuilder UseLambdaServer(this IWebHostBuilder hostBuilder)
        {
            return hostBuilder.ConfigureServices(services =>
            {
                Utilities.EnsureLambdaServerRegistered(services);
            });
        }
    }
}
