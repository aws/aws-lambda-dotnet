namespace BlueprintBaseName._1

open System.Text

open Amazon.Lambda.Core
open Amazon.Lambda.KinesisEvents
open System.IO

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
()

type Function() =

    /// <summary>
    /// A function to process Kinesis events
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    member _this.FunctionHandler (kinesisEvent: KinesisEvent) (context: ILambdaContext) =

        context.Logger.LogLine(sprintf "Beginning to process % i records..." kinesisEvent.Records.Count)

        let getRecordContent (data : MemoryStream) =
            use reader = new StreamReader(data, Encoding.ASCII)
            reader.ReadToEnd()

        let printRecord (record : KinesisEvent.KinesisEventRecord ) = 
            context.Logger.LogLine(sprintf "Event ID: %s" record.EventId)
            context.Logger.LogLine(sprintf "Event Name: %s" record.EventName)
            context.Logger.LogLine("Record Data:")
            context.Logger.LogLine(getRecordContent record.Kinesis.Data)           

        kinesisEvent.Records
            |> Seq.iter (fun x -> printRecord x)

        context.Logger.LogLine("Stream processing complete.")
