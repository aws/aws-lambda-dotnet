namespace BlueprintBaseName.1.Tests

open System
open System.IO
open System.Collections.Generic

open Xunit
open Amazon.Lambda.TestUtilities

open Amazon.Lambda.DynamoDBEvents
open Amazon.Lambda.TestUtilities
open Amazon.DynamoDBv2
open Amazon.DynamoDBv2.Model

open BlueprintBaseName.1


module FunctionTest =    

    [<Fact>]
    let ``Test Reading DynamoDB Stream Event``() =

        let streamRecord = new StreamRecord(
                                ApproximateCreationDateTime = DateTime.Now,
                                StreamViewType = StreamViewType.NEW_AND_OLD_IMAGES,
                                Keys = new Dictionary<string, AttributeValue>(),
                                NewImage = new Dictionary<string, AttributeValue>(),
                                OldImage = new Dictionary<string, AttributeValue>()
                                )

        streamRecord.Keys.Add("id", new AttributeValue(S = "MyId"))

        streamRecord.NewImage.Add("field1", new AttributeValue(S = "NewValue"))
        streamRecord.NewImage.Add("field2", new AttributeValue(S = "AnotherNewValue"))

        streamRecord.OldImage.Add("field1", new AttributeValue(S = "OldValue"))
        streamRecord.OldImage.Add("field2", new AttributeValue(S = "AnotherOldValue"))


        let ddbEvent = new DynamoDBEvent(Records = new List<DynamoDBEvent.DynamodbStreamRecord>())
        ddbEvent.Records.Add(new DynamoDBEvent.DynamodbStreamRecord(
                                    EventID = "id-foo",
                                    EventName = OperationType.INSERT,
                                    AwsRegion = "us-west-2",
                                    Dynamodb = streamRecord
                                    ))

        let lambdaFunction = Function()

        let testLogger = new TestLambdaLogger()
        let context = TestLambdaContext(Logger = testLogger)
        lambdaFunction.FunctionHandler ddbEvent  context
        
        Assert.Contains("id-foo", testLogger.Buffer.ToString())
        Assert.Contains("Stream processing complete", testLogger.Buffer.ToString())
    
    [<EntryPoint>]
    let main argv = 0