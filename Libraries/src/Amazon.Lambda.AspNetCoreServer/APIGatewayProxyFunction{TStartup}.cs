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
        /// <inheritdoc/>
        protected override IWebHostBuilder CreateWebHostBuilder() =>
            base.CreateWebHostBuilder().UseStartup<TStartup>();
    }
}
