using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

namespace TestServerlessApp
{
    public class SimpleCalculator_DivideAsync_Generated
    {
        private readonly ServiceProvider serviceProvider;

        public SimpleCalculator_DivideAsync_Generated()
        {
            var services = new ServiceCollection();

            // By default, Lambda function class is added to the service container using the singleton lifetime
            // To use a different lifetime, specify the lifetime in Startup.ConfigureServices(IServiceCollection) method.
            services.AddSingleton<SimpleCalculator>();

            var startup = new TestServerlessApp.Startup();
            startup.ConfigureServices(services);
            serviceProvider = services.BuildServiceProvider();
        }

        public async Task<APIGatewayProxyResponse> DivideAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            // Create a scope for every request,
            // this allows creating scoped dependencies without creating a scope manually.
            using var scope = serviceProvider.CreateScope();
            var simpleCalculator = scope.ServiceProvider.GetRequiredService<SimpleCalculator>();

            var first = default(int);
            if (request.PathParameters?.ContainsKey("x") == true)
            {
                first = (int)Convert.ChangeType(request.PathParameters["x"], typeof(int));
            }

            var second = default(int);
            if (request.PathParameters?.ContainsKey("y") == true)
            {
                second = (int)Convert.ChangeType(request.PathParameters["y"], typeof(int));
            }

            var response = await simpleCalculator.DivideAsync(first, second);

            var body = System.Text.Json.JsonSerializer.Serialize(response);

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
    }
}