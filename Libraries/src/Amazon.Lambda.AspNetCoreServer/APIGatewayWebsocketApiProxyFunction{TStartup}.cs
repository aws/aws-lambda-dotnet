using Microsoft.AspNetCore.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace Amazon.Lambda.AspNetCoreServer
{
    /// <summary>
    /// APIGatewayWebsocketApiV2ProxyFunction is the base class that is implemented in a ASP.NET Core Web API. The derived class implements
    /// the Init method similar to Main function in the ASP.NET Core and provides typed Startup. The function handler for
    /// the Lambda function will point to this base class FunctionHandlerAsync method.
    /// </summary>
    /// <typeparam name ="TStartup">The type containing the startup methods for the application.</typeparam>
    public abstract class APIGatewayWebsocketApiProxyFunction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TStartup> : APIGatewayWebsocketApiProxyFunction where TStartup : class
    {
        /// <summary>
        /// Default Constructor. The ASP.NET Core Framework will be initialized as part of the construction.
        /// </summary>
        protected APIGatewayWebsocketApiProxyFunction()
            : base()
        {

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="startupMode">Configure when the ASP.NET Core framework will be initialized</param>
        protected APIGatewayWebsocketApiProxyFunction(StartupMode startupMode)
            : base(startupMode)
        {

        }

        /// <inheritdoc/>
        protected override void Init(IWebHostBuilder builder)
        {
            builder.UseStartup<TStartup>();
        }
    }
}
