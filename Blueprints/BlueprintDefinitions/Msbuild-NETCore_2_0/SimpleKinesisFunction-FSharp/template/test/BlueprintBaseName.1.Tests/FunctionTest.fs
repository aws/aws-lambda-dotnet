namespace BlueprintBaseName._1.Tests

open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Text
open System.Threading.Tasks

open Xunit

open Amazon
open Amazon.Lambda.Core
open Amazon.Lambda.KinesisEvents
open Amazon.Lambda.TestUtilities

open BlueprintBaseName._1

module FunctionTest =    

    [<Fact>]
    let ``Test Reading Kinesis Stream Event``() =
        
        let kinesisEvent =  new KinesisEvent(Records = new List<KinesisEvent.KinesisEventRecord>())
        kinesisEvent.Records.Add(new KinesisEvent.KinesisEventRecord(
                                        EventId = "id-foo",
                                        EventName = "id-name",
                                        AwsRegion = "us-west-2",
                                        Kinesis = new KinesisEvent.Record(
                                                        ApproximateArrivalTimestamp = DateTime.Now,
                                                        Data = new MemoryStream(Encoding.UTF8.GetBytes("Hello World Kinesis Record"))
                                                    )))
        
        let lambdaFunction = Function()

        let testLogger = new TestLambdaLogger()
        let context = TestLambdaContext(Logger = testLogger)
        lambdaFunction.FunctionHandler kinesisEvent  context
        
        Assert.Contains("id-foo", testLogger.Buffer.ToString())
        Assert.Contains("Stream processing complete", testLogger.Buffer.ToString())