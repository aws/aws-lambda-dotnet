using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class SimpleCalculator_DivideAsync_Generated
    {
        private readonly ServiceProvider serviceProvider;

        public SimpleCalculator_DivideAsync_Generated()
        {
            SetExecutionEnvironment();
            var services = new ServiceCollection();

            // By default, Lambda function class is added to the service container using the singleton lifetime
            // To use a different lifetime, specify the lifetime in Startup.ConfigureServices(IServiceCollection) method.
            services.AddSingleton<SimpleCalculator>();

            var startup = new TestServerlessApp.Startup();
            startup.ConfigureServices(services);
            serviceProvider = services.BuildServiceProvider();
        }

        public async System.Threading.Tasks.Task<Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse> DivideAsync(Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest request, Amazon.Lambda.Core.ILambdaContext context)
        {
            // Create a scope for every request,
            // this allows creating scoped dependencies without creating a scope manually.
            using var scope = serviceProvider.CreateScope();
            var simpleCalculator = scope.ServiceProvider.GetRequiredService<SimpleCalculator>();

            var validationErrors = new List<string>();

            var first = default(int);
            if (request.PathParameters?.ContainsKey("x") == true)
            {
                try
                {
                    first = (int)Convert.ChangeType(request.PathParameters["x"], typeof(int));
                }
                catch (Exception e) when (e is InvalidCastException || e is FormatException || e is OverflowException || e is ArgumentException)
                {
                    validationErrors.Add($"Value {request.PathParameters["x"]} at 'x' failed to satisfy constraint: {e.Message}");
                }
            }

            var second = default(int);
            if (request.PathParameters?.ContainsKey("y") == true)
            {
                try
                {
                    second = (int)Convert.ChangeType(request.PathParameters["y"], typeof(int));
                }
                catch (Exception e) when (e is InvalidCastException || e is FormatException || e is OverflowException || e is ArgumentException)
                {
                    validationErrors.Add($"Value {request.PathParameters["y"]} at 'y' failed to satisfy constraint: {e.Message}");
                }
            }

            // return 400 Bad Request if there exists a validation error
            if (validationErrors.Any())
            {
                return new Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse
                {
                    Body = @$"{{""message"": ""{validationErrors.Count} validation error(s) detected: {string.Join(",", validationErrors)}""}}",
                    Headers = new Dictionary<string, string>
                    {
                        {"Content-Type", "application/json"},
                        {"x-amzn-ErrorType", "ValidationException"}
                    },
                    StatusCode = 400
                };
            }

            var response = await simpleCalculator.DivideAsync(first, second);

            var body = System.Text.Json.JsonSerializer.Serialize(response);

            return new Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse
            {
                Body = body,
                Headers = new Dictionary<string, string>
                {
                    {"Content-Type", "application/json"}
                },
                StatusCode = 200
            };
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

            envValue.Append("amazon-lambda-annotations_0.5.0.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}