using Amazon.Lambda.AspNetCoreServer.Internal;
using Amazon.Lambda.AspNetCoreServer.Hosting.Internal;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Enum for the possible event sources that will send HTTP request into the ASP.NET Core Lambda function.
    /// </summary>
    public enum LambdaEventSource 
    {
        /// <summary>
        /// API Gateway REST API
        /// </summary>
        RestApi, 

        /// <summary>
        /// API Gateway HTTP API
        /// </summary>
        HttpApi, 

        /// <summary>
        /// ELB Application Load Balancer
        /// </summary>
        ApplicationLoadBalancer
    }

    /// <summary>
    /// Extension methods to IServiceCollection. 
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add the ability to run the ASP.NET Core Lambda function in AWS Lambda. If the project is not running in Lambda 
        /// this method will do nothing allowing the normal Kestrel webserver to host the application.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="eventSource"></param>
        /// <returns></returns>
        public static IServiceCollection AddAWSLambdaHosting(this IServiceCollection services, LambdaEventSource eventSource)
        {
            // Not running in Lambda so exit and let Kestrel be the web server
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME")))
                return services;

            var serverType = eventSource switch
            {
                LambdaEventSource.HttpApi => typeof(APIGatewayHttpApiV2LambdaRuntimeSupportServer),
                LambdaEventSource.RestApi => typeof(APIGatewayRestApiLambdaRuntimeSupportServer),
                LambdaEventSource.ApplicationLoadBalancer => typeof(ApplicationLoadBalancerLambdaRuntimeSupportServer),
                _ => throw new ArgumentException($"Event source type {eventSource} unknown")
            };

            Utilities.EnsureLambdaServerRegistered(services, serverType);
            return services;
        }
    }
}
