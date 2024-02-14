using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class Greeter_SayHello_Generated
    {
        private readonly ServiceProvider serviceProvider;

        public Greeter_SayHello_Generated()
        {
            SetExecutionEnvironment();
            var services = new ServiceCollection();

            // By default, Lambda function class is added to the service container using the singleton lifetime
            // To use a different lifetime, specify the lifetime in Startup.ConfigureServices(IServiceCollection) method.
            services.AddSingleton<Greeter>();
            services.AddSingleton<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>();

            var startup = new TestServerlessApp.Startup();
            startup.ConfigureServices(services);
            serviceProvider = services.BuildServiceProvider();
        }

        public Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse SayHello(Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest __request__, Amazon.Lambda.Core.ILambdaContext __context__)
        {
            // Create a scope for every request,
            // this allows creating scoped dependencies without creating a scope manually.
            using var scope = serviceProvider.CreateScope();
            var greeter = scope.ServiceProvider.GetRequiredService<Greeter>();
            var serializer = scope.ServiceProvider.GetRequiredService<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>();

            var validationErrors = new List<string>();

            var firstNames = default(System.Collections.Generic.IEnumerable<string>);
            if (__request__.MultiValueQueryStringParameters?.ContainsKey("names") == true)
            {
                firstNames = __request__.MultiValueQueryStringParameters["names"]
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

            greeter.SayHello(firstNames, __request__, __context__);

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

            envValue.Append("amazon-lambda-annotations_1.2.0.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}