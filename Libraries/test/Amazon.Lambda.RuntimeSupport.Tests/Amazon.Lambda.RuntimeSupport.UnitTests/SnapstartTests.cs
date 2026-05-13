using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using static Amazon.Lambda.RuntimeSupport.Bootstrap.Constants;

namespace Amazon.Lambda.RuntimeSupport.UnitTests;
public class SnapstartTests 
{
    readonly TestHandler _testFunction;
    readonly TestInitializer _testInitializer;
    readonly TestRuntimeApiClient _testRuntimeApiClient;
    readonly TestEnvironmentVariables _environmentVariables;

    public SnapstartTests()
    {
        _environmentVariables = new TestEnvironmentVariables();
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            {
                RuntimeApiHeaders.HeaderAwsRequestId, new List<string> { "request_id" }
            },
            {
                RuntimeApiHeaders.HeaderInvokedFunctionArn, new List<string> { "invoked_function_arn" }
            }
        };
        _testRuntimeApiClient = new TestRuntimeApiClient(_environmentVariables, headers);
        _testInitializer = new TestInitializer();
        _testFunction = new TestHandler();
    }

    [Fact]
    public async Task VerifyRestoreNextIsCalledWhenSnapstartIsEnabled()
    {
        using var bootstrap =
            new LambdaBootstrap(_testFunction.BaseHandlerAsync, _testInitializer.InitializeTrueAsync, configuration: new LambdaBootstrapConfiguration(false, true));
        bootstrap.Client = _testRuntimeApiClient;
        await bootstrap.RunAsync(_testFunction.CancellationSource.Token);
        Assert.True(_testRuntimeApiClient.RestoreNextInvocationAsyncCalled); 
    }

    [Fact]
    public async Task VerifyRestoreNextIsNotCalledWhenSnapstartIsDisabled()
    {
        using var bootstrap =
            new LambdaBootstrap(_testFunction.BaseHandlerAsync, _testInitializer.InitializeTrueAsync, configuration: new LambdaBootstrapConfiguration(false, false));
        bootstrap.Client = _testRuntimeApiClient;
        Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE, AWS_LAMBDA_INITIALIZATION_TYPE_ON_DEMAND);
        await bootstrap.RunAsync(_testFunction.CancellationSource.Token);
        Assert.False(_testRuntimeApiClient.RestoreNextInvocationAsyncCalled);
    }


    [Fact]
    public async Task VerifyInitializeErrorIsCalledWhenExceptionInBeforeSnapshotCallables()
    {
        using var bootstrap =
            new LambdaBootstrap(_testFunction.BaseHandlerAsync, _testInitializer.InitializeTrueAsync, configuration: new LambdaBootstrapConfiguration(false, true));
        bootstrap.Client = _testRuntimeApiClient;
        Core.SnapshotRestore.RegisterBeforeSnapshot(
            () => throw new Exception("Error in Before snapshot callable 1"));
        Core.SnapshotRestore.RegisterBeforeSnapshot(() => ValueTask.CompletedTask);
        await bootstrap.RunAsync(_testFunction.CancellationSource.Token);
        Assert.True(_testRuntimeApiClient.ReportInitializationErrorAsyncExceptionCalled);
    }

    [Fact]
    public async Task VerifyRestoreErrorIsCalledWhenExceptionInAfterRestoreCallables()
    {
        using (var bootstrap =
               new LambdaBootstrap(_testFunction.BaseHandlerAsync, _testInitializer.InitializeTrueAsync,  new LambdaBootstrapConfiguration(false, true)))
        {
            bootstrap.Client = _testRuntimeApiClient;
            Core.SnapshotRestore.RegisterAfterRestore(() => ValueTask.CompletedTask);
            Core.SnapshotRestore.RegisterAfterRestore(() => throw new Exception("Error in After restore callable 1"));
            await bootstrap.RunAsync(_testFunction.CancellationSource.Token);
            Assert.True(_testRuntimeApiClient.ReportRestoreErrorAsyncCalled);
        }
    }
}
