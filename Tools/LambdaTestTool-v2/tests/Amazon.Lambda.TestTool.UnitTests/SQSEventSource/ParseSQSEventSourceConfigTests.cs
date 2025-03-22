using Amazon.Lambda.TestTool.Processes.SQSEventSource;
using Xunit;

namespace Amazon.Lambda.TestTool.UnitTests.SQSEventSource;

public class ParseSQSEventSourceConfigTests
{
    [Fact]
    public void ParseValidJsonObject()
    {
        string json = """
{
    "QueueUrl" : "https://amazonsqs/queueurl",
    "FunctionName" : "LambdaFunction",
    "BatchSize" : 5,
    "DisableMessageDelete" : true,
    "LambdaRuntimeApi" : "http://localhost:7777/",
    "Profile" : "beta",
    "Region" : "us-east-23",
    "VisibilityTimeout" : 50
}
""";

        var configs = SQSEventSourceProcess.LoadSQSEventSourceConfig(json);
        Assert.Single(configs);
        Assert.Equal("https://amazonsqs/queueurl", configs[0].QueueUrl);
        Assert.Equal("LambdaFunction", configs[0].FunctionName);
        Assert.Equal(5, configs[0].BatchSize);
        Assert.True(configs[0].DisableMessageDelete);
        Assert.Equal("http://localhost:7777/", configs[0].LambdaRuntimeApi);
        Assert.Equal("beta", configs[0].Profile);
        Assert.Equal("us-east-23", configs[0].Region);
        Assert.Equal(50, configs[0].VisibilityTimeout);

    }

    [Fact]
    public void ParseInvalidJsonObject()
    {
        string json = """
{
    "aaa"
}
""";

        Assert.Throws<InvalidOperationException>(() => SQSEventSourceProcess.LoadSQSEventSourceConfig(json));
    }


    [Fact]
    public void ParseValidJsonArray()
    {
        string json = """
[
    {
        "QueueUrl" : "https://amazonsqs/queueurl",
        "FunctionName" : "LambdaFunction",
        "BatchSize" : 5,
        "DisableMessageDelete" : true,
        "LambdaRuntimeApi" : "http://localhost:7777/",
        "Profile" : "beta",
        "Region" : "us-east-23",
        "VisibilityTimeout" : 50
    },
    {
        "QueueUrl" : "https://amazonsqs/queueurl",
        "FunctionName" : "LambdaFunction",
        "BatchSize" : 5,
        "DisableMessageDelete" : true,
        "LambdaRuntimeApi" : "http://localhost:7777/",
        "Profile" : "beta",
        "Region" : "us-east-23",
        "VisibilityTimeout" : 50
    }
]
""";

        var configs = SQSEventSourceProcess.LoadSQSEventSourceConfig(json);
        Assert.Equal(2, configs.Count);

        foreach (var config in configs)
        {            
            Assert.Equal("https://amazonsqs/queueurl", config.QueueUrl);
            Assert.Equal("LambdaFunction", config.FunctionName);
            Assert.Equal(5, config.BatchSize);
            Assert.True(config.DisableMessageDelete);
            Assert.Equal("http://localhost:7777/", config.LambdaRuntimeApi);
            Assert.Equal("beta", config.Profile);
            Assert.Equal("us-east-23", config.Region);
            Assert.Equal(50, config.VisibilityTimeout);
        }
    }

    [Fact]
    public void ParseInvalidJsonArray()
    {
        string json = """
[
    {"aaa"}
]
""";

        Assert.Throws<InvalidOperationException>(() => SQSEventSourceProcess.LoadSQSEventSourceConfig(json));
    }

    [Fact]
    public void ParseQueueUrl()
    {
        var configs = SQSEventSourceProcess.LoadSQSEventSourceConfig("https://amazonsqs/queueurl");
        Assert.Single(configs);
        Assert.Equal("https://amazonsqs/queueurl", configs[0].QueueUrl);
    }

    [Fact]
    public void ParseKeyPairs()
    {
        var configs = SQSEventSourceProcess.LoadSQSEventSourceConfig(
            "QueueUrl=https://amazonsqs/queueurl ,functionName =LambdaFunction, batchSize=5, DisableMessageDelete=true," +
            "LambdaRuntimeApi=http://localhost:7777/ ,Profile=beta,Region=us-east-23,VisibilityTimeout=50");

        Assert.Single(configs);
        Assert.Equal("https://amazonsqs/queueurl", configs[0].QueueUrl);
        Assert.Equal("LambdaFunction", configs[0].FunctionName);
        Assert.Equal(5, configs[0].BatchSize);
        Assert.True(configs[0].DisableMessageDelete);
        Assert.Equal("http://localhost:7777/", configs[0].LambdaRuntimeApi);
        Assert.Equal("beta", configs[0].Profile);
        Assert.Equal("us-east-23", configs[0].Region);
        Assert.Equal(50, configs[0].VisibilityTimeout);
    }

    [Theory]
    [InlineData("novalue")]
    [InlineData("BatchSize=noint")]
    [InlineData("VisibilityTimeout=noint")]
    [InlineData("DisableMessageDelete=nobool")]
    [InlineData("QueueUrl=https://amazonsqs/queueurl FunctionName =LambdaFunction")]
    public void InvalidKeyPairString(string keyPairConfig)
    {
        Assert.Throws<InvalidOperationException>(() => SQSEventSourceProcess.LoadSQSEventSourceConfig(keyPairConfig));
    }
}
