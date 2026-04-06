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
        /// Uses the raw ApplicationLoadBalancerRequest (pass-through mode).
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
        /// Uses the raw ApplicationLoadBalancerRequest (pass-through mode).
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

        /// <summary>
        /// Greeting endpoint that uses FromQuery and FromHeader parameter binding.
        /// Demonstrates ALB functions with any number of parameters using FromX attributes.
        /// </summary>
        [LambdaFunction(ResourceName = "ALBGreeting", MemorySize = 256, Timeout = 15)]
        [ALBApi("@ALBTestListener", "/greeting", 3)]
        public ApplicationLoadBalancerResponse Greeting(
            [FromQuery] string name,
            [FromHeader(Name = "X-Custom-Header")] string customHeader,
            ILambdaContext context)
        {
            context.Logger.LogInformation($"Greeting endpoint hit. Name: {name}, Header: {customHeader}");

            return new ApplicationLoadBalancerResponse
            {
                StatusCode = 200,
                StatusDescription = "200 OK",
                IsBase64Encoded = false,
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                },
                Body = $"{{\"message\": \"Hello {name}!\", \"customHeader\": \"{customHeader}\"}}"
            };
        }

        /// <summary>
        /// Endpoint that uses FromBody to deserialize JSON request body.
        /// Demonstrates ALB function with body deserialization.
        /// </summary>
        [LambdaFunction(ResourceName = "ALBCreateItem", MemorySize = 256, Timeout = 15)]
        [ALBApi("@ALBTestListener", "/items", 4, HttpMethod = "POST")]
        public ApplicationLoadBalancerResponse CreateItem(
            [FromBody] string body,
            ILambdaContext context)
        {
            context.Logger.LogInformation($"CreateItem endpoint hit. Body: {body}");

            return new ApplicationLoadBalancerResponse
            {
                StatusCode = 201,
                StatusDescription = "201 Created",
                IsBase64Encoded = false,
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                },
                Body = $"{{\"created\": true, \"body\": \"{body}\"}}"
            };
        }
    }
}
