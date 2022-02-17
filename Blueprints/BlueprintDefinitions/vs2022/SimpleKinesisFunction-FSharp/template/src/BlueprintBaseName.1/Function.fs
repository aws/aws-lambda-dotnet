namespace BlueprintBaseName._1


open Amazon.Lambda.Core
open Amazon.Lambda.KinesisEvents

open System.IO
open System.Text


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>)>]
()


type Function() =
    /// <summary>
    /// A function to process Kinesis events
    /// </summary>
    /// <param name="kinesisEvent"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    member __.FunctionHandler (kinesisEvent: KinesisEvent) (context: ILambdaContext) =
        sprintf "Beginning to process % i records..." kinesisEvent.Records.Count
        |> context.Logger.LogInformation

        let getRecordContent (data: MemoryStream) =
            use reader = new StreamReader(data, Encoding.ASCII)
            reader.ReadToEnd()

        let printRecord (record: KinesisEvent.KinesisEventRecord) =
            context.Logger.LogInformation(sprintf "Event ID: %s" record.EventId)
            context.Logger.LogInformation(sprintf "Event Name: %s" record.EventName)
            context.Logger.LogInformation("Record Data:")
            context.Logger.LogInformation(getRecordContent record.Kinesis.Data)

        kinesisEvent.Records
        |> Seq.iter printRecord

        context.Logger.LogInformation("Stream processing complete.")
