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
    public class ComplexCalculator_Add_Generated
    {
        private readonly ServiceProvider serviceProvider;

        public ComplexCalculator_Add_Generated()
        {
            SetExecutionEnvironment();
            var services = new ServiceCollection();

            // By default, Lambda function class is added to the service container using the singleton lifetime
            // To use a different lifetime, specify the lifetime in Startup.ConfigureServices(IServiceCollection) method.
            services.AddSingleton<ComplexCalculator>();
            services.AddSingleton<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>();

            var startup = new TestServerlessApp.Startup();
            startup.ConfigureServices(services);
            serviceProvider = services.BuildServiceProvider();
        }

        public System.IO.Stream Add(Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest __request__, Amazon.Lambda.Core.ILambdaContext __context__)
        {
            // Create a scope for every request,
            // this allows creating scoped dependencies without creating a scope manually.
            using var scope = serviceProvider.CreateScope();
            var complexCalculator = scope.ServiceProvider.GetRequiredService<ComplexCalculator>();
            var serializer = scope.ServiceProvider.GetRequiredService<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>();

            var complexNumbers = __request__.Body;

            var result = complexCalculator.Add(complexNumbers, __context__, __request__);
            var memoryStream = new MemoryStream();
            serializer.Serialize(result, memoryStream);
            memoryStream.Position = 0;

            // convert stream to string
            StreamReader reader = new StreamReader(memoryStream);
            var body = reader.ReadToEnd();
            var response = new Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse
            {
                Body = body,
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                {
                    {"Content-Type", "application/json"}
                }
            };

            var responseStream = new MemoryStream();
            JsonSerializer.Serialize(responseStream, response, typeof(Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse));
            responseStream.Position = 0;
            return responseStream;
        }

        private static void SetExecutionEnvironment()
        {
            const string envName = "AWS_EXECUTION_ENV";

            var envValue = new StringBuilder();

            // If there is an existing execution environment variable add the annotations package as a suffix.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envName)))
            {
                envValue.Append($"{Environment.GetEnvironmentVariable(envName)}_");
            }

            envValue.Append("lib/amazon-lambda-annotations#1.3.1.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}