﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class SimpleCalculator_Pi_Generated
    {
        private readonly ServiceProvider serviceProvider;

        public SimpleCalculator_Pi_Generated()
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

        public double Pi()
        {
            // Create a scope for every request,
            // this allows creating scoped dependencies without creating a scope manually.
            using var scope = serviceProvider.CreateScope();
            var simpleCalculator = scope.ServiceProvider.GetRequiredService<SimpleCalculator>();

            var simpleCalculatorService = scope.ServiceProvider.GetRequiredService<TestServerlessApp.Services.ISimpleCalculatorService>();
            return simpleCalculator.Pi(simpleCalculatorService);
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

            envValue.Append("amazon-lambda-annotations_0.13.3.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}