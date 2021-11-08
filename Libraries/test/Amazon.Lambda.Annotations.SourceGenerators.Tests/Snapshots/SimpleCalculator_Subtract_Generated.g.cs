﻿using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

namespace TestServerlessApp
{
    public class SimpleCalculator_Subtract_Generated
    {
        private readonly ServiceProvider serviceProvider;

        public SimpleCalculator_Subtract_Generated()
        {
            var services = new ServiceCollection();

            // By default, Lambda function class is added to the service container using the singleton lifetime
            // To use a different lifetime, specify the lifetime in Startup.ConfigureServices(IServiceCollection) method.
            services.AddSingleton<SimpleCalculator>();

            var startup = new TestServerlessApp.Startup();
            startup.ConfigureServices(services);
            serviceProvider = services.BuildServiceProvider();
        }

        public APIGatewayProxyResponse Subtract(Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest request, Amazon.Lambda.Core.ILambdaContext context)
        {
            // Create a scope for every request,
            // this allows creating scoped dependencies without creating a scope manually.
            using var scope = serviceProvider.CreateScope();
            var simpleCalculator = scope.ServiceProvider.GetRequiredService<SimpleCalculator>();

            var x = default(int);
            if (request.Headers?.ContainsKey("x") == true)
            {
                x = (int)Convert.ChangeType(request.Headers["x"], typeof(int));
            }

            var y = default(int);
            if (request.Headers?.ContainsKey("y") == true)
            {
                y = (int)Convert.ChangeType(request.Headers["y"], typeof(int));
            }

            var simpleCalculatorService = scope.ServiceProvider.GetRequiredService<TestServerlessApp.Services.ISimpleCalculatorService>();
            var response = simpleCalculator.Subtract(x, y, simpleCalculatorService);
            return response;
        }
    }
}