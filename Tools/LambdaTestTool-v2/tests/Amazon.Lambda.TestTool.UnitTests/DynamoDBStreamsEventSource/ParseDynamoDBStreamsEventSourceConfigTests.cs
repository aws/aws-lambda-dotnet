using Amazon.Lambda.TestTool.Processes.DynamoDBStreamsEventSource;
using Xunit;

namespace Amazon.Lambda.TestTool.UnitTests.DynamoDBStreamsEventSource;

public class ParseDynamoDBStreamsEventSourceConfigTests
{
    [Fact]
    public void ParseValidJsonObject()
    {
        string json = """
{
    "TableName" : "my-table",
    "FunctionName" : "LambdaFunction",
    "BatchSize" : 50,
    "LambdaRuntimeApi" : "http://localhost:7777/",
    "Profile" : "beta",
    "Region" : "us-east-23"
}
""";

        var configs = DynamoDBStreamsEventSourceProcess.LoadDynamoDBStreamsEventSourceConfig(json);
        Assert.Single(configs);
        Assert.Equal("my-table", configs[0].TableName);
        Assert.Equal("LambdaFunction", configs[0].FunctionName);
        Assert.Equal(50, configs[0].BatchSize);
        Assert.Equal("http://localhost:7777/", configs[0].LambdaRuntimeApi);
        Assert.Equal("beta", configs[0].Profile);
        Assert.Equal("us-east-23", configs[0].Region);
    }

    [Fact]
    public void ParseInvalidJsonObject()
    {
        string json = """
{
    "aaa"
}
""";

        Assert.Throws<InvalidOperationException>(() => DynamoDBStreamsEventSourceProcess.LoadDynamoDBStreamsEventSourceConfig(json));
    }

    [Fact]
    public void ParseValidJsonArray()
    {
        string json = """
[
    {
        "TableName" : "table-1",
        "FunctionName" : "Function1",
        "BatchSize" : 25
    },
    {
        "TableName" : "table-2",
        "FunctionName" : "Function2",
        "BatchSize" : 75
    }
]
""";

        var configs = DynamoDBStreamsEventSourceProcess.LoadDynamoDBStreamsEventSourceConfig(json);
        Assert.Equal(2, configs.Count);
        Assert.Equal("table-1", configs[0].TableName);
        Assert.Equal("Function1", configs[0].FunctionName);
        Assert.Equal(25, configs[0].BatchSize);
        Assert.Equal("table-2", configs[1].TableName);
        Assert.Equal("Function2", configs[1].FunctionName);
        Assert.Equal(75, configs[1].BatchSize);
    }

    [Fact]
    public void ParseInvalidJsonArray()
    {
        string json = """
[
    {"aaa"}
]
""";

        Assert.Throws<InvalidOperationException>(() => DynamoDBStreamsEventSourceProcess.LoadDynamoDBStreamsEventSourceConfig(json));
    }

    [Fact]
    public void ParseKeyPairs()
    {
        var configs = DynamoDBStreamsEventSourceProcess.LoadDynamoDBStreamsEventSourceConfig(
            "TableName=my-table ,functionName =LambdaFunction, batchSize=50," +
            "LambdaRuntimeApi=http://localhost:7777/ ,Profile=beta,Region=us-east-23");

        Assert.Single(configs);
        Assert.Equal("my-table", configs[0].TableName);
        Assert.Equal("LambdaFunction", configs[0].FunctionName);
        Assert.Equal(50, configs[0].BatchSize);
        Assert.Equal("http://localhost:7777/", configs[0].LambdaRuntimeApi);
        Assert.Equal("beta", configs[0].Profile);
        Assert.Equal("us-east-23", configs[0].Region);
    }

    [Theory]
    [InlineData("novalue")]
    [InlineData("BatchSize=noint")]
    public void InvalidKeyPairString(string keyPairConfig)
    {
        Assert.Throws<InvalidOperationException>(() => DynamoDBStreamsEventSourceProcess.LoadDynamoDBStreamsEventSourceConfig(keyPairConfig));
    }
}
