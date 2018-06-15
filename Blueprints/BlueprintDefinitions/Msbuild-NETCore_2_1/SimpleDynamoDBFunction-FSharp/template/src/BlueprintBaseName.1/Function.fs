namespace BlueprintBaseName.1

open Newtonsoft.Json

open Amazon.Lambda.Core
open Amazon.Lambda.DynamoDBEvents
open System.IO

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
()

type Function() =

    let jsonSerializer = new JsonSerializer()

    /// <summary>
    /// A simple function to print out the DynamoDB stream event
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    member _this.FunctionHandler (dynamoEvent: DynamoDBEvent) (context: ILambdaContext) =

        context.Logger.LogLine(sprintf "Beginning to process %i records..." dynamoEvent.Records.Count)

        let serializeStreamRecord streamRecord =
            use writer = new StringWriter()
            jsonSerializer.Serialize(writer, streamRecord)
            writer.ToString()

        let printRecord (record : DynamoDBEvent.DynamodbStreamRecord)= 
            context.Logger.LogLine(sprintf "Event ID: %s" record.EventID)
            context.Logger.LogLine(sprintf "Event Name: %s" record.EventName.Value)

            context.Logger.LogLine("DynamoDB Record:")
            context.Logger.LogLine(serializeStreamRecord record.Dynamodb)


        dynamoEvent.Records 
            |> Seq.iter(fun x -> printRecord x)

        context.Logger.LogLine("Stream processing complete.")
            