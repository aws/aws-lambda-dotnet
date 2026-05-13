using Xunit;


using Amazon.Lambda.DynamoDBEvents;
using Amazon.Lambda.TestUtilities;




namespace BlueprintBaseName._1.Tests;

public class FunctionTest
{
    [Fact]
    public void TestFunction()
    {
        DynamoDBEvent evnt = new DynamoDBEvent
        {
            Records = new List<DynamoDBEvent.DynamodbStreamRecord>
            {
                new DynamoDBEvent.DynamodbStreamRecord
                {
                    AwsRegion = "us-west-2",
                    Dynamodb = new DynamoDBEvent.StreamRecord
                    {
                        ApproximateCreationDateTime = DateTime.Now,
                        Keys = new Dictionary<string, DynamoDBEvent.AttributeValue> { {"id", new DynamoDBEvent.AttributeValue { S = "MyId" } } },
                        NewImage = new Dictionary<string, DynamoDBEvent.AttributeValue> { { "field1", new DynamoDBEvent.AttributeValue { S = "NewValue" } }, { "field2", new DynamoDBEvent.AttributeValue { S = "AnotherNewValue" } } },
                        OldImage = new Dictionary<string, DynamoDBEvent.AttributeValue> { { "field1", new DynamoDBEvent.AttributeValue { S = "OldValue" } }, { "field2", new DynamoDBEvent.AttributeValue { S = "AnotherOldValue" } } },
                        StreamViewType = "NEW_AND_OLD_IMAGES"
                    }
                }
            }
        };

        var context = new TestLambdaContext();
        var function = new Function();

        function.FunctionHandler(evnt, context);

        var testLogger = context.Logger as TestLambdaLogger;
        Assert.Contains("Stream processing complete", testLogger?.Buffer.ToString());
    }  
}