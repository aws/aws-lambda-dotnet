using System;
using System.Linq;
using Amazon.Lambda.AspNetCoreServer.Hosting.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Test
{
    public class TestBedrockServiceCollectionExtensions
    {
        [Fact]
        public void TestTryLambdaSetupWithBedrockAgentApi()
        {
            var services = new ServiceCollection();
            Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "TestFunction");
            
            try
            {
                services.AddAWSLambdaHosting(LambdaEventSource.BedrockAgentApi);
                var descriptor = services.FirstOrDefault(d => 
                    d.ServiceType == typeof(Microsoft.AspNetCore.Hosting.Server.IServer) && 
                    d.ImplementationType == typeof(BedrockAgentApiLambdaRuntimeSupportServer));
                
                Assert.NotNull(descriptor);
            }
            finally
            {
                Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
            }
        }

        [Fact]
        public void TestAddAWSLambdaHostingWithBedrockAgentApi()
        {
            var services = new ServiceCollection();
            Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "TestFunction");
            
            try
            {
                services.AddAWSLambdaHosting(LambdaEventSource.BedrockAgentApi);
                
                var descriptor = services.FirstOrDefault(d => 
                    d.ServiceType == typeof(Microsoft.AspNetCore.Hosting.Server.IServer) && 
                    d.ImplementationType == typeof(BedrockAgentApiLambdaRuntimeSupportServer));
                
                Assert.NotNull(descriptor);
            }
            finally
            {
                Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
            }
        }

        [Fact]
        public void TestAddAWSLambdaHostingWithoutLambdaEnvironment()
        {
            var services = new ServiceCollection();
            Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
            
            services.AddAWSLambdaHosting(LambdaEventSource.BedrockAgentApi);
            
            var descriptor = services.FirstOrDefault(d => 
                d.ServiceType == typeof(Microsoft.AspNetCore.Hosting.Server.IServer) && 
                d.ImplementationType == typeof(BedrockAgentApiLambdaRuntimeSupportServer));
            
            Assert.Null(descriptor);
        }
    }
} 
