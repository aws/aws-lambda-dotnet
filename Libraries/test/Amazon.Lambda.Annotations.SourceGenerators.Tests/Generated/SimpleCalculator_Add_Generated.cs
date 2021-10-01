using System;
using System.Collections.Generic;

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
            var services = new ServiceCollection();

            // By default, Lambda function class is added to the service container using the singleton lifetime
            // To use a different lifetime, specify the lifetime in Startup.ConfigureServices(IServiceCollection) method.
            services.AddSingleton<TestServerlessApp.SimpleCalculator>();

            var startup = new TestServerlessApp.Startup();
            startup.ConfigureServices(services);
            serviceProvider = services.BuildServiceProvider();
        }

        public APIGatewayProxyResponse Add(APIGatewayProxyRequest request, ILambdaContext _context)
        {
            // Create a scope for every request,
            // this allows creating scoped dependencies without creating a scope manually.
            using var scope = serviceProvider.CreateScope();

            var simpleCalculator = scope.ServiceProvider.GetRequiredService<TestServerlessApp.SimpleCalculator>();

            var response = simpleCalculator.Add();
            var body = response.ToString();

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = body,
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "text/plain" }
                }
            };
        }
    }
}