using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class Greeter_SayHelloAsync_Generated
    {
        private static readonly ServiceProvider serviceProvider;

        static Greeter_SayHelloAsync_Generated()     
        {
            SetExecutionEnvironment();
            var services = new ServiceCollection();

            // By default, Lambda function class is added to the service container using the singleton lifetime
            // To use a different lifetime, specify the lifetime in Startup.ConfigureServices(IServiceCollection) method.
            services.AddSingleton<Greeter>();

            var startup = new TestServerlessApp.Startup();
            startup.ConfigureServices(services);
            serviceProvider = services.BuildServiceProvider();
        }

        public static async System.Threading.Tasks.Task<Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse> SayHelloAsync(Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest __request__, Amazon.Lambda.Core.ILambdaContext __context__)
        {
            // Create a scope for every request,
            // this allows creating scoped dependencies without creating a scope manually.
            using var scope = serviceProvider.CreateScope();
            var greeter = scope.ServiceProvider.GetRequiredService<Greeter>();

            var validationErrors = new List<string>();

            var firstNames = default(System.Collections.Generic.IEnumerable<string>);
            if (__request__.MultiValueHeaders?.Any(x => string.Equals(x.Key, "names", StringComparison.OrdinalIgnoreCase)) == true)
            {
                firstNames = __request__.MultiValueHeaders.First(x => string.Equals(x.Key, "names", StringComparison.OrdinalIgnoreCase)).Value
                    .Select(q =>
                    {
                        try
                        {
                            return (string)Convert.ChangeType(q, typeof(string));
                        }
                        catch (Exception e) when (e is InvalidCastException || e is FormatException || e is OverflowException || e is ArgumentException)
                        {
                        validationErrors.Add($"Value {q} at 'names' failed to satisfy constraint: {e.Message}");
                            return default;
                        }
                    })
                    .ToList();
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
                return errorResult;
            }

            await greeter.SayHelloAsync(firstNames);

            return new Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse
            {
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

            envValue.Append("amazon-lambda-annotations_1.0.0.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}