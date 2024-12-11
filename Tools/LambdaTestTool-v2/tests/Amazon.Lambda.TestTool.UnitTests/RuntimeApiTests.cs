using Amazon.Runtime;
using Amazon.Lambda.Model;
using Amazon.Lambda.TestTool.Services;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestTool.Processes;
using Amazon.Lambda.TestTool.Commands.Settings;

namespace Amazon.Lambda.TestTool.UnitTests;

public class RuntimeApiTests
{
    public RuntimeApiTests()
    {
        // Set this environment variable so anytime we start the LambdaBootstrap from RuntimeSupport it exists after processing an event.
        System.Environment.SetEnvironmentVariable("AWS_LAMBDA_DOTNET_DEBUG_RUN_ONCE", "true");
    }

    [Fact]
    public async Task AddEventToDataStore()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var options = new RunCommandSettings();
        options.Port = 9000;
        var testToolProcess = TestToolProcess.Startup(options, cancellationTokenSource.Token);
        try
        {
            var lambdaClient = ConstructLambdaServiceClient(testToolProcess.ServiceUrl);
            var invokeFunction = new InvokeRequest
            {
                FunctionName = "FunctionFoo",
                Payload = "\"hello\""
            };

            await lambdaClient.InvokeAsync(invokeFunction);

            var dataStore = testToolProcess.Services.GetService(typeof(IRuntimeApiDataStore)) as IRuntimeApiDataStore;
            Assert.NotNull(dataStore);
            Assert.Single(dataStore.QueuedEvents);

            var handlerCalled = false;
            var handler = (string input, ILambdaContext context) =>
            {
                handlerCalled = true;
                return input.ToUpper();
            };

            System.Environment.SetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API", $"{options.Host}:{options.Port}");
            await LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                    .Build()
                    .RunAsync();

            Assert.True(handlerCalled);
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();
            await testToolProcess.RunningTask;
        }
    }

    private IAmazonLambda ConstructLambdaServiceClient(string url)
    {
        var config = new AmazonLambdaConfig
        {
            ServiceURL = url
        };

        // We don't need real credentials because we are not calling the real Lambda service.
        var credentials = new BasicAWSCredentials("accessKeyId", "secretKey");
        return new AmazonLambdaClient(credentials, config);
    }
}