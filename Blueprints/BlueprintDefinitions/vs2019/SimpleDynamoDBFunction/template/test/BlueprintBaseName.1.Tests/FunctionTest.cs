using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

using Amazon;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.Lambda.TestUtilities;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using BlueprintBaseName._1;

namespace BlueprintBaseName._1.Tests
{
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
                        Dynamodb = new StreamRecord
                        {
                            ApproximateCreationDateTime = DateTime.Now,
                            Keys = new Dictionary<string, AttributeValue> { {"id", new AttributeValue { S = "MyId" } } },
                            NewImage = new Dictionary<string, AttributeValue> { { "field1", new AttributeValue { S = "NewValue" } }, { "field2", new AttributeValue { S = "AnotherNewValue" } } },
                            OldImage = new Dictionary<string, AttributeValue> { { "field1", new AttributeValue { S = "OldValue" } }, { "field2", new AttributeValue { S = "AnotherOldValue" } } },
                            StreamViewType = StreamViewType.NEW_AND_OLD_IMAGES
                        }
                    }
                }
            };


            var context = new TestLambdaContext();
            var function = new Function();

            function.FunctionHandler(evnt, context);

            var testLogger = context.Logger as TestLambdaLogger;
			Assert.Contains("Stream processing complete", testLogger.Buffer.ToString());
        }  
    }
}
