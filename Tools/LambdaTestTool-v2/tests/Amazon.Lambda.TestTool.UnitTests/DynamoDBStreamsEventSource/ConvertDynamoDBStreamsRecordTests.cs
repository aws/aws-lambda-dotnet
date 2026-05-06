using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.Lambda.TestTool.Processes.DynamoDBStreamsEventSource;
using Xunit;
using Record = Amazon.DynamoDBStreams.Model.Record;

namespace Amazon.Lambda.TestTool.UnitTests.DynamoDBStreamsEventSource;

public class ConvertDynamoDBStreamsRecordTests
{
    private const string TestStreamArn = "arn:aws:dynamodb:us-west-2:123456789012:table/my-table/stream/2024-01-01T00:00:00.000";

    [Fact]
    public void ConvertBasicRecord()
    {
        var record = new Record
        {
            EventID = "event-123",
            EventName = new Amazon.DynamoDBStreams.OperationType("INSERT"),
            EventVersion = "1.1",
            EventSource = "aws:dynamodb",
            Dynamodb = new StreamRecord
            {
                ApproximateCreationDateTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                SequenceNumber = "111111111111111111111",
                SizeBytes = 256,
                StreamViewType = new StreamViewType("NEW_AND_OLD_IMAGES"),
                Keys = new Dictionary<string, AttributeValue>
                {
                    ["Id"] = new AttributeValue { S = "key-1" }
                },
                NewImage = new Dictionary<string, AttributeValue>
                {
                    ["Id"] = new AttributeValue { S = "key-1" },
                    ["Name"] = new AttributeValue { S = "Test Item" }
                }
            }
        };

        var result = DynamoDBStreamsEventSourceBackgroundService.ConvertToLambdaRecord(record, TestStreamArn);

        Assert.Equal("event-123", result.EventID);
        Assert.Equal("INSERT", result.EventName);
        Assert.Equal("aws:dynamodb", result.EventSource);
        Assert.Equal(TestStreamArn, result.EventSourceArn);
        Assert.Equal("1.1", result.EventVersion);
        Assert.Equal("us-west-2", result.AwsRegion);

        Assert.NotNull(result.Dynamodb);
        Assert.Equal("111111111111111111111", result.Dynamodb.SequenceNumber);
        Assert.Equal(256, result.Dynamodb.SizeBytes);
        Assert.Equal("NEW_AND_OLD_IMAGES", result.Dynamodb.StreamViewType);

        Assert.Single(result.Dynamodb.Keys);
        Assert.Equal("key-1", result.Dynamodb.Keys["Id"].S);

        Assert.Equal(2, result.Dynamodb.NewImage.Count);
        Assert.Equal("key-1", result.Dynamodb.NewImage["Id"].S);
        Assert.Equal("Test Item", result.Dynamodb.NewImage["Name"].S);
    }

    [Fact]
    public void ConvertRecordWithAllAttributeTypes()
    {
        var record = new Record
        {
            EventID = "event-456",
            EventName = new Amazon.DynamoDBStreams.OperationType("MODIFY"),
            EventVersion = "1.1",
            Dynamodb = new StreamRecord
            {
                Keys = new Dictionary<string, AttributeValue>
                {
                    ["Id"] = new AttributeValue { S = "key-1" }
                },
                NewImage = new Dictionary<string, AttributeValue>
                {
                    ["StringAttr"] = new AttributeValue { S = "hello" },
                    ["NumberAttr"] = new AttributeValue { N = "42" },
                    ["BoolAttr"] = new AttributeValue { BOOL = true },
                    ["NullAttr"] = new AttributeValue { NULL = true },
                    ["ListAttr"] = new AttributeValue
                    {
                        L = new List<AttributeValue>
                        {
                            new AttributeValue { S = "item1" },
                            new AttributeValue { N = "2" }
                        }
                    },
                    ["MapAttr"] = new AttributeValue
                    {
                        M = new Dictionary<string, AttributeValue>
                        {
                            ["nested"] = new AttributeValue { S = "value" }
                        }
                    },
                    ["StringSetAttr"] = new AttributeValue { SS = new List<string> { "a", "b" } },
                    ["NumberSetAttr"] = new AttributeValue { NS = new List<string> { "1", "2" } }
                }
            }
        };

        var result = DynamoDBStreamsEventSourceBackgroundService.ConvertToLambdaRecord(record, TestStreamArn);

        var newImage = result.Dynamodb.NewImage;
        Assert.Equal("hello", newImage["StringAttr"].S);
        Assert.Equal("42", newImage["NumberAttr"].N);
        Assert.True(newImage["BoolAttr"].BOOL);
        Assert.True(newImage["NullAttr"].NULL);
        Assert.Equal(2, newImage["ListAttr"].L.Count);
        Assert.Equal("item1", newImage["ListAttr"].L[0].S);
        Assert.Equal("value", newImage["MapAttr"].M["nested"].S);
        Assert.Equal(new List<string> { "a", "b" }, newImage["StringSetAttr"].SS);
        Assert.Equal(new List<string> { "1", "2" }, newImage["NumberSetAttr"].NS);
    }

    [Fact]
    public void ConvertRecordWithUserIdentity()
    {
        var record = new Record
        {
            EventID = "event-789",
            EventName = new Amazon.DynamoDBStreams.OperationType("REMOVE"),
            EventVersion = "1.1",
            UserIdentity = new Identity
            {
                PrincipalId = "dynamodb.amazonaws.com",
                Type = "Service"
            },
            Dynamodb = new StreamRecord
            {
                Keys = new Dictionary<string, AttributeValue>
                {
                    ["Id"] = new AttributeValue { S = "expired-item" }
                }
            }
        };

        var result = DynamoDBStreamsEventSourceBackgroundService.ConvertToLambdaRecord(record, TestStreamArn);

        Assert.NotNull(result.UserIdentity);
        Assert.Equal("dynamodb.amazonaws.com", result.UserIdentity.PrincipalId);
        Assert.Equal("Service", result.UserIdentity.Type);
    }

    [Fact]
    public void ConvertMultipleRecords()
    {
        var records = new List<Record>
        {
            new Record
            {
                EventID = "event-1",
                EventName = new Amazon.DynamoDBStreams.OperationType("INSERT"),
                EventVersion = "1.1",
                Dynamodb = new StreamRecord
                {
                    Keys = new Dictionary<string, AttributeValue>
                    {
                        ["Id"] = new AttributeValue { S = "key-1" }
                    }
                }
            },
            new Record
            {
                EventID = "event-2",
                EventName = new Amazon.DynamoDBStreams.OperationType("MODIFY"),
                EventVersion = "1.1",
                Dynamodb = new StreamRecord
                {
                    Keys = new Dictionary<string, AttributeValue>
                    {
                        ["Id"] = new AttributeValue { S = "key-2" }
                    }
                }
            }
        };

        var result = DynamoDBStreamsEventSourceBackgroundService.ConvertToLambdaRecords(records, TestStreamArn);

        Assert.Equal(2, result.Count);
        Assert.Equal("event-1", result[0].EventID);
        Assert.Equal("event-2", result[1].EventID);
    }
}
