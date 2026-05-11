using Amazon.Lambda.DurableExecution;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class ConfigTests
{
    [Fact]
    public void SerializationContext_RecordEquality()
    {
        var ctx1 = new SerializationContext("op-1", "arn:aws:lambda:us-east-1:123:function:my-func");
        var ctx2 = new SerializationContext("op-1", "arn:aws:lambda:us-east-1:123:function:my-func");
        Assert.Equal(ctx1, ctx2);
    }
}
