using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Internal;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class EnumsTests
{
    [Fact]
    public void InvocationStatus_HasExpectedValues()
    {
        Assert.Equal(0, (int)InvocationStatus.Succeeded);
        Assert.Equal(1, (int)InvocationStatus.Failed);
        Assert.Equal(2, (int)InvocationStatus.Pending);
    }

    [Fact]
    public void OperationTypes_HasExpectedConstants()
    {
        Assert.Equal("STEP", OperationTypes.Step);
        Assert.Equal("WAIT", OperationTypes.Wait);
        Assert.Equal("CALLBACK", OperationTypes.Callback);
        Assert.Equal("CHAINED_INVOKE", OperationTypes.ChainedInvoke);
        Assert.Equal("CONTEXT", OperationTypes.Context);
        Assert.Equal("EXECUTION", OperationTypes.Execution);
    }

    [Fact]
    public void OperationStatuses_HasExpectedConstants()
    {
        Assert.Equal("STARTED", OperationStatuses.Started);
        Assert.Equal("SUCCEEDED", OperationStatuses.Succeeded);
        Assert.Equal("FAILED", OperationStatuses.Failed);
        Assert.Equal("PENDING", OperationStatuses.Pending);
        Assert.Equal("CANCELLED", OperationStatuses.Cancelled);
        Assert.Equal("READY", OperationStatuses.Ready);
        Assert.Equal("STOPPED", OperationStatuses.Stopped);
    }
}
