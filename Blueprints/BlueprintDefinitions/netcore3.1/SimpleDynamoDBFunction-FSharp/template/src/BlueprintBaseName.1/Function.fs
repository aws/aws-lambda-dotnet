namespace BlueprintBaseName._1


open Amazon.Lambda.Core
open Amazon.Lambda.DynamoDBEvents

open System.IO
open Newtonsoft.Json


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
()


type Function() =
    let jsonSerializer = JsonSerializer()

    /// <summary>
    /// A simple function to print out the DynamoDB stream event
    /// </summary>
    /// <param name="dynamoEvent"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    member __.FunctionHandler (dynamoEvent: DynamoDBEvent) (context: ILambdaContext) =
        sprintf "Beginning to process %i records..." dynamoEvent.Records.Count
        |> context.Logger.LogLine

        let serializeStreamRecord streamRecord =
            use writer = new StringWriter()
            jsonSerializer.Serialize(writer, streamRecord)
            writer.ToString()

        let printRecord (record: DynamoDBEvent.DynamodbStreamRecord) =
            context.Logger.LogLine(sprintf "Event ID: %s" record.EventID)
            context.Logger.LogLine(sprintf "Event Name: %s" record.EventName.Value)
            context.Logger.LogLine("DynamoDB Record:")
            context.Logger.LogLine(serializeStreamRecord record.Dynamodb)

        dynamoEvent.Records
        |> Seq.iter printRecord

        context.Logger.LogLine("Stream processing complete.")
