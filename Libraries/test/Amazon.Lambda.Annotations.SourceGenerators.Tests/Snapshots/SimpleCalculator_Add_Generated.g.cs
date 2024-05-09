using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Amazon.Lambda.Core;
using System.Text.Json;

namespace TestServerlessApp
{
    public class SimpleCalculator_Add_Generated
    {
        private readonly ServiceProvider serviceProvider;

        public SimpleCalculator_Add_Generated()
        {
            SetExecutionEnvironment();
            var services = new ServiceCollection();

            // By default, Lambda function class is added to the service container using the singleton lifetime
            // To use a different lifetime, specify the lifetime in Startup.ConfigureServices(IServiceCollection) method.
            services.AddSingleton<SimpleCalculator>();
            services.AddSingleton<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>();

            var startup = new TestServerlessApp.Startup();
            startup.ConfigureServices(services);
            serviceProvider = services.BuildServiceProvider();
        }

        public System.IO.Stream Add(Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest __request__, Amazon.Lambda.Core.ILambdaContext __context__)
        {
            // Create a scope for every request,
            // this allows creating scoped dependencies without creating a scope manually.
            using var scope = serviceProvider.CreateScope();
            var simpleCalculator = scope.ServiceProvider.GetRequiredService<SimpleCalculator>();
            var serializer = scope.ServiceProvider.GetRequiredService<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>();

            var validationErrors = new List<string>();

            var x = default(int);
            if (__request__.QueryStringParameters?.ContainsKey("x") == true)
            {
                try
                {
                    x = (int)Convert.ChangeType(__request__.QueryStringParameters["x"], typeof(int));
                }
                catch (Exception e) when (e is InvalidCastException || e is FormatException || e is OverflowException || e is ArgumentException)
                {
                    validationErrors.Add($"Value {__request__.QueryStringParameters["x"]} at 'x' failed to satisfy constraint: {e.Message}");
                }
            }

            var y = default(int);
            if (__request__.QueryStringParameters?.ContainsKey("y") == true)
            {
                try
                {
                    y = (int)Convert.ChangeType(__request__.QueryStringParameters["y"], typeof(int));
                }
                catch (Exception e) when (e is InvalidCastException || e is FormatException || e is OverflowException || e is ArgumentException)
                {
                    validationErrors.Add($"Value {__request__.QueryStringParameters["y"]} at 'y' failed to satisfy constraint: {e.Message}");
                }
            }

            // return 400 Bad Request if there exists a validation error
            if (validationErrors.Any())
            {
                var errorResult = new Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse
                {
                    Body = @$"{{""message"": ""{validationErrors.Count} validation error(s) detected: {string.Join(",", validationErrors)}""}}",
                    Headers = new Dictionary<string, string>
                    {
                        {"Content-Type", "application/json"},
                        {"x-amzn-ErrorType", "ValidationException"}
                    },
                    StatusCode = 400
                };

                var errorStream = new MemoryStream();
                JsonSerializer.Serialize(errorStream, errorResult, typeof(Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse));
                errorStream.Position = 0;
                return errorStream;
            }
            var result = simpleCalculator.Add(x, y);
            var body = result.ToString();
            var response = new Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse
            {
                Body = body,
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                {
                    {"Content-Type", "application/json"}
                }
            };

            var responseStream = new MemoryStream();
            JsonSerializer.Serialize(responseStream, response, typeof(Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse));
            responseStream.Position = 0;
            return responseStream;
        }

        private static void SetExecutionEnvironment()
        {
            const string envName = "AWS_EXECUTION_ENV";

            var envValue = new StringBuilder();

            // If there is an existing execution environment variable add the annotations package as a suffix.
            if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envName)))
            {
                envValue.Append($"{Environment.GetEnvironmentVariable(envName)}_");
            }

            envValue.Append("lib/amazon-lambda-annotations#1.3.1.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}