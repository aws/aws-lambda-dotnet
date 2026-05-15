using Microsoft.AspNetCore.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace Amazon.Lambda.AspNetCoreServer
{
    /// <summary>
    /// Strongly-typed variant of <see cref="APIGatewayWebsocketApiProxyFunction"/> that wires up an ASP.NET Core Startup class.
    /// The Lambda function handler should point at the inherited <c>FunctionHandlerAsync</c> method.
    /// </summary>
    /// <typeparam name ="TStartup">The type containing the startup methods for the application.</typeparam>
    public abstract class APIGatewayWebsocketApiProxyFunction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TStartup> : APIGatewayWebsocketApiProxyFunction where TStartup : class
    {
        /// <summary>
        /// Default constructor. The ASP.NET Core framework is initialized as part of construction.
        /// </summary>
        protected APIGatewayWebsocketApiProxyFunction()
            : base()
        {
        }

        /// <summary>
        /// Constructor that lets the caller defer ASP.NET Core framework initialization until the first request.
        /// </summary>
        /// <param name="startupMode">Configures when the ASP.NET Core framework is initialized.</param>
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
