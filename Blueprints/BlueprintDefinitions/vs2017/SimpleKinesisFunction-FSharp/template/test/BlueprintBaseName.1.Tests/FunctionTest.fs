namespace BlueprintBaseName._1.Tests


open Xunit
open Amazon.Lambda.KinesisEvents
open Amazon.Lambda.TestUtilities

open System
open System.Collections.Generic
open System.IO
open System.Text

open BlueprintBaseName._1


module FunctionTest =
    [<Fact>]
    let ``Test Reading Kinesis Stream Event``() =
        let kinesisRecords = [
            KinesisEvent.KinesisEventRecord(
                EventId = "id-foo",
                EventName = "id-name",
                AwsRegion = "us-west-2",
                Kinesis = KinesisEvent.Record(
                    ApproximateArrivalTimestamp = DateTime.Now,
                    Data = new MemoryStream(Encoding.UTF8.GetBytes("Hello World Kinesis Record"))
                )
            )
        ]

        let kinesisEvent = KinesisEvent(Records = List(kinesisRecords))
        let lambdaFunction = Function()
        let testLogger = TestLambdaLogger()
        let context = TestLambdaContext(Logger = testLogger)
        lambdaFunction.FunctionHandler kinesisEvent context

        Assert.Contains("id-foo", testLogger.Buffer.ToString())
        Assert.Contains("Stream processing complete", testLogger.Buffer.ToString())

    [<EntryPoint>]
    let main _ = 0
