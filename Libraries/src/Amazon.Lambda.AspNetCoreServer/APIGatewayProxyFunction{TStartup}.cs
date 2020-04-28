using Microsoft.AspNetCore.Hosting;

namespace Amazon.Lambda.AspNetCoreServer
{
    /// <summary>
    /// ApiGatewayProxyFunction is the base class that is implemented in a ASP.NET Core Web API. The derived class implements
    /// the Init method similar to Main function in the ASP.NET Core and provides typed Startup. The function handler for
    /// the Lambda function will point to this base class FunctionHandlerAsync method.
    /// </summary>
    /// <typeparam name ="TStartup">The type containing the startup methods for the application.</typeparam>
    public abstract class APIGatewayProxyFunction<TStartup> : APIGatewayProxyFunction where TStartup : class
    {
        /// <summary>
        /// Default Constructor. The ASP.NET Core Framework will be initialized as part of the construction.
        /// </summary>
        protected APIGatewayProxyFunction()
            : base()
        {

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="startupMode">Configure when the ASP.NET Core framework will be initialized</param>
        protected APIGatewayProxyFunction(StartupMode startupMode)
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
