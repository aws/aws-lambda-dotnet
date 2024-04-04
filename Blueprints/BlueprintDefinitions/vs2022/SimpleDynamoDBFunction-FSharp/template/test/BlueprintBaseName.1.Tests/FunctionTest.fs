namespace BlueprintBaseName._1.Tests

open Xunit
open Amazon.Lambda.TestUtilities

open Amazon.Lambda.DynamoDBEvents

open System
open System.Collections.Generic

open BlueprintBaseName._1


module FunctionTest =
    [<Fact>]
    let ``Test Reading DynamoDB Stream Event``() =
        let keys = dict [ ("id", DynamoDBEvent.AttributeValue(S = "MyId")) ]

        let newImage = dict [
                            ("field1", DynamoDBEvent.AttributeValue(S = "NewValue"))
                            ("field2", DynamoDBEvent.AttributeValue(S = "AnotherNewValue"))
                        ]

        let oldImages = dict [
                            ("field1", DynamoDBEvent.AttributeValue(S = "OldValue"))
                            ("field2", DynamoDBEvent.AttributeValue(S = "AnotherOldValue"))
                        ]

        let streamRecord =
            DynamoDBEvent.StreamRecord(
                ApproximateCreationDateTime = DateTime.Now,
                StreamViewType = "NEW_AND_OLD_IMAGES",
                Keys = Dictionary(keys),
                NewImage = Dictionary(newImage),
                OldImage = Dictionary(oldImages)
            )

        let ddbStreamRecords = [
            DynamoDBEvent.DynamodbStreamRecord(
                EventID = "id-foo",
                EventName = "INSERT",
                AwsRegion = "us-west-2",
                Dynamodb = streamRecord
            )
        ]

        let ddbEvent = DynamoDBEvent(Records = List(ddbStreamRecords))
        let lambdaFunction = Function()
        let testLogger = TestLambdaLogger()
        let context = TestLambdaContext(Logger = testLogger)
        lambdaFunction.FunctionHandler ddbEvent context

        Assert.Contains("Stream processing complete", testLogger.Buffer.ToString())

    [<EntryPoint>]
    let main _ = 0
