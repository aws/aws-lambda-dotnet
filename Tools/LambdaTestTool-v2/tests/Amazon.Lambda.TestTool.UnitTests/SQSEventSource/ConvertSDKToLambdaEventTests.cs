using Amazon.Lambda.TestTool.Processes.SQSEventSource;
using Amazon.SQS.Model;
using Xunit;

namespace Amazon.Lambda.TestTool.UnitTests.SQSEventSource;

public class ConvertSDKToLambdaEventTests
{
    [Fact]
    public void ConvertSDKMessageFull()
    {
        var sdkMessage = new Message
        {
            Attributes = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } },
            Body = "theBody",
            MD5OfBody = "theBodyMD5",
            MD5OfMessageAttributes = "attributesMD5",
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                { "key1", new MessageAttributeValue{StringValue = "value1", DataType = "String"} },
                { "key2", new MessageAttributeValue{BinaryValue = new MemoryStream(), DataType = "Binary"} }
            },
            MessageId = "id",
            ReceiptHandle = "receiptHandle"
        };

        var eventMessage = SQSEventSourceBackgroundService.ConvertToLambdaMessage(sdkMessage, "us-west-2", "queueArn");
        Assert.Equal("us-west-2", eventMessage.AwsRegion);
        Assert.Equal("queueArn", eventMessage.EventSourceArn);
        Assert.Equal("aws:sqs", eventMessage.EventSource);

        Assert.Equal(sdkMessage.Attributes, eventMessage.Attributes);
        Assert.Equal("theBody", eventMessage.Body);
        Assert.Equal("theBodyMD5", eventMessage.Md5OfBody);
        Assert.Equal("attributesMD5", eventMessage.Md5OfMessageAttributes);
        Assert.Equal("id", eventMessage.MessageId);
        Assert.Equal("receiptHandle", eventMessage.ReceiptHandle);

        Assert.Equal(2, eventMessage.MessageAttributes.Count);

        Assert.Equal("value1", eventMessage.MessageAttributes["key1"].StringValue);
        Assert.Null(eventMessage.MessageAttributes["key1"].BinaryValue);
        Assert.Equal("String", eventMessage.MessageAttributes["key1"].DataType);

        Assert.Null(eventMessage.MessageAttributes["key2"].StringValue);
        Assert.NotNull(eventMessage.MessageAttributes["key2"].BinaryValue);
        Assert.Equal("Binary", eventMessage.MessageAttributes["key2"].DataType);
    }

    [Fact]
    public void ConvertSDKMessageWithNullCollections()
    {
        var sdkMessage = new Message
        {
            Attributes = null,
            Body = "theBody",
            MD5OfBody = "theBodyMD5",
            MD5OfMessageAttributes = "attributesMD5",
            MessageAttributes = null,
            MessageId = "id",
            ReceiptHandle = "receiptHandle"
        };

        var eventMessage = SQSEventSourceBackgroundService.ConvertToLambdaMessage(sdkMessage, "us-west-2", "queueArn");
        Assert.Equal("us-west-2", eventMessage.AwsRegion);
        Assert.Equal("queueArn", eventMessage.EventSourceArn);
        Assert.Equal("aws:sqs", eventMessage.EventSource);

        Assert.Equal("theBody", eventMessage.Body);
        Assert.Equal("theBodyMD5", eventMessage.Md5OfBody);
        Assert.Equal("attributesMD5", eventMessage.Md5OfMessageAttributes);
        Assert.Equal("id", eventMessage.MessageId);
        Assert.Equal("receiptHandle", eventMessage.ReceiptHandle);

        Assert.Null(eventMessage.Attributes);
        Assert.Null(eventMessage.MessageAttributes);
    }
}
