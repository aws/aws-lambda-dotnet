using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.BedrockAgentEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Amazon.Lambda.AspNetCoreServer.Hosting.Internal;

namespace Amazon.Lambda.AspNetCoreServer.Test
{
    public class TestBedrockAgentApiHosting
    {
        [Fact]
        public void TestBedrockAgentApiLambdaRuntimeSupportServerCreation()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddLambdaLogger());
            services.AddSingleton<ILambdaSerializer>(new DefaultLambdaJsonSerializer());
            var serviceProvider = services.BuildServiceProvider();

            var server = new BedrockAgentApiLambdaRuntimeSupportServer(serviceProvider);

            Assert.NotNull(server);
        }

        [Fact]
        public void TestBedrockAgentApiHandlerWrapperCreation()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            services.AddSingleton<ILambdaSerializer>(new DefaultLambdaJsonSerializer());
            var serviceProvider = services.BuildServiceProvider();
            var server = new BedrockAgentApiLambdaRuntimeSupportServer(serviceProvider);

            var methodInfo = typeof(BedrockAgentApiLambdaRuntimeSupportServer)
                .GetMethod("CreateHandlerWrapper", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var handlerWrapper = methodInfo.Invoke(server, new object[] { serviceProvider });

            Assert.NotNull(handlerWrapper);
        }

        [Fact]
        public void TestBedrockAgentApiMinimalApiCreation()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddLambdaLogger());
            services.AddSingleton<ILambdaSerializer>(new DefaultLambdaJsonSerializer());
            var serviceProvider = services.BuildServiceProvider();

            var minimalApi = new BedrockAgentApiLambdaRuntimeSupportServer.BedrockAgentApiMinimalApi(serviceProvider);

            Assert.NotNull(minimalApi);
        }

        [Fact]
        public async Task TestBedrockAgentApiMinimalApiHandlerMethod()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddLambdaLogger());
            services.AddSingleton<ILambdaSerializer>(new DefaultLambdaJsonSerializer());
            var serviceProvider = services.BuildServiceProvider();
            var minimalApi = new BedrockAgentApiLambdaRuntimeSupportServer.BedrockAgentApiMinimalApi(serviceProvider);
            var context = new TestLambdaContext();

            var request = new BedrockAgentApiRequest
            {
                MessageVersion = "1.0",
                Agent = new AgentInfo
                {
                    Name = "TestAgent",
                    Id = "agent-12345",
                    Alias = "TestAlias",
                    Version = "1.0"
                },
                InputText = "Test",
                SessionId = "session-12345",
                ActionGroup = "TestApi",
                ApiPath = "/api/test",
                HttpMethod = "GET",
                Parameters = new List<Parameter>(),
                SessionAttributes = new Dictionary<string, string>
                {
                    ["key1"] = "value1"
                },
                PromptSessionAttributes = new Dictionary<string, string>
                {
                    ["key2"] = "value2"
                }
            };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await minimalApi.FunctionHandlerAsync(request, context);
            });

            Assert.Contains("application", exception.Message.ToLower());
        }
    }
} 
