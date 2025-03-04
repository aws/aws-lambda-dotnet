using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Tests.Common.Retries;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.TestTool.IntegrationTests;

[Collection("ApiGatewayTests")]
public class BinaryResponseTests : BaseApiGatewayTest
{
    public BinaryResponseTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }

    [RetryFact]
    public async Task TestLambdaBinaryResponse()
    {
        var ports = await GetFreePorts();
        var lambdaPort = ports.lambdaPort;
        var apiGatewayPort = ports.apiGatewayPort;
        
        CancellationTokenSource = new CancellationTokenSource();
        CancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(120));
        var consoleError = Console.Error;
        
        try
        {
            Console.SetError(TextWriter.Null);
            await StartTestToolProcessAsync(ApiGatewayEmulatorMode.HttpV2, "binaryfunction", lambdaPort, apiGatewayPort, CancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);

            var handler = (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
            {
                TestOutputHelper.WriteLine($"TestLambdaBinaryResponse");
                // Create a simple binary pattern (for example, counting bytes from 0 to 255)
                byte[] binaryData = new byte[256];
                for (int i = 0; i < 256; i++)
                {
                    binaryData[i] = (byte)i;
                }

                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 200,
                    Body = Convert.ToBase64String(binaryData),
                    IsBase64Encoded = true,
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/octet-stream" }
                    }
                };
            };

            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"localhost:{lambdaPort}/binaryfunction")
                .Build()
                .RunAsync(CancellationTokenSource.Token);

            var response = await TestEndpoint("binaryfunction", apiGatewayPort, httpMethod: "POST");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);

            var binaryData = await response.Content.ReadAsByteArrayAsync();
            Assert.Equal(256, binaryData.Length);
            for (var i = 0; i < 256; i++)
            {
                Assert.Equal((byte)i, binaryData[i]);
            }
        }
        finally
        {
            await CancellationTokenSource.CancelAsync();
            Console.SetError(consoleError);
        }
    }
} 