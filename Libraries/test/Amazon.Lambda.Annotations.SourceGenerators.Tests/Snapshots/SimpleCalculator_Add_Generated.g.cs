using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

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

            var startup = new TestServerlessApp.Startup();
            startup.ConfigureServices(services);
            serviceProvider = services.BuildServiceProvider();
        }

        public Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse Add(Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest request, Amazon.Lambda.Core.ILambdaContext context)
        {
            // Create a scope for every request,
            // this allows creating scoped dependencies without creating a scope manually.
            using var scope = serviceProvider.CreateScope();
            var simpleCalculator = scope.ServiceProvider.GetRequiredService<SimpleCalculator>();

            var x = default(int);
            if (request.QueryStringParameters?.ContainsKey("x") == true)
            {
                x = (int)Convert.ChangeType(request.QueryStringParameters["x"], typeof(int));
            }

            var y = default(int);
            if (request.QueryStringParameters?.ContainsKey("y") == true)
            {
                y = (int)Convert.ChangeType(request.QueryStringParameters["y"], typeof(int));
            }

            var response = simpleCalculator.Add(x, y);

            var body = response.ToString();

            return new APIGatewayProxyResponse
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

            envValue.Append("amazon-lambda-annotations_0.1.0.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}