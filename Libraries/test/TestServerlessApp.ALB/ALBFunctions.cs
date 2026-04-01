using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.ALB;
using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.Lambda.Core;
using System.Collections.Generic;

namespace TestServerlessApp.ALB
{
    public class ALBFunctions
    {
        /// <summary>
        /// Hello endpoint - returns a greeting message with the request path.
        /// </summary>
        [LambdaFunction(ResourceName = "ALBHello", MemorySize = 256, Timeout = 15)]
        [ALBApi("@ALBTestListener", "/hello", 1)]
        public ApplicationLoadBalancerResponse Hello(ApplicationLoadBalancerRequest request, ILambdaContext context)
        {
            context.Logger.LogInformation($"Hello endpoint hit. Path: {request.Path}");

            return new ApplicationLoadBalancerResponse
            {
                StatusCode = 200,
                StatusDescription = "200 OK",
                IsBase64Encoded = false,
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                },
                Body = $"{{\"message\": \"Hello from ALB Lambda!\", \"path\": \"{request.Path}\"}}"
            };
        }

        /// <summary>
        /// Health check endpoint for ALB target group health checks.
        /// </summary>
        [LambdaFunction(ResourceName = "ALBHealth", MemorySize = 128, Timeout = 5)]
        [ALBApi("@ALBTestListener", "/health", 2)]
        public ApplicationLoadBalancerResponse Health(ApplicationLoadBalancerRequest request, ILambdaContext context)
        {
            return new ApplicationLoadBalancerResponse
            {
                StatusCode = 200,
                StatusDescription = "200 OK",
                IsBase64Encoded = false,
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                },
                Body = "{\"status\": \"healthy\"}"
            };
        }
    }
}
