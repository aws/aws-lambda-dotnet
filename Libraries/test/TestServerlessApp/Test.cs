﻿using System;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace TestServerlessApp
{
    public class ComplexCalculator_Add_Generated2
    {
        private readonly ServiceProvider serviceProvider;

        public ComplexCalculator_Add_Generated2()
        {
            var services = new ServiceCollection();

            // By default, Lambda function class is added to the service container using the scoped lifetime
            // because web dependencies are normally scoped to the client request. To use a different lifetime,
            // specify the lifetime in Startup.ConfigureServices(IServiceCollection) method.
            services.AddScoped<TestServerlessApp.ComplexCalculator>();
            var startup = new TestServerlessApp.Startup();
            startup.ConfigureServices(services);
            serviceProvider = services.BuildServiceProvider();
        }

        public APIGatewayProxyResponse Add(APIGatewayProxyRequest request, ILambdaContext _context)
        {
            // Create a scope for every request,
            // this allows creating scoped dependencies without creating a scope manually.
            using var scope = serviceProvider.CreateScope();

            var complexCalculator = scope.ServiceProvider.GetRequiredService<TestServerlessApp.ComplexCalculator>();
            var response = complexCalculator.Add();
            var body = System.Text.Json.JsonSerializer.Serialize(response);

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