namespace BlueprintBaseName._1


open Amazon.Lambda.Core
open Amazon.Lambda.DynamoDBEvents

open System.IO


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>)>]
()


type Function() =

    /// <summary>
    /// A simple function to print out the DynamoDB stream event
    /// </summary>
    /// <param name="dynamoEvent"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    member __.FunctionHandler (dynamoEvent: DynamoDBEvent) (context: ILambdaContext) =
        sprintf "Beginning to process %i records..." dynamoEvent.Records.Count
        |> context.Logger.LogLine

        let processRecord (record: DynamoDBEvent.DynamodbStreamRecord) =
            context.Logger.LogLine(sprintf "Event ID: %s" record.EventID)
            context.Logger.LogLine(sprintf "Event Name: %s" record.EventName.Value)
            // TODO: Add business logic processing the record.Dynamodb object.

        dynamoEvent.Records
        |> Seq.iter processRecord

        context.Logger.LogLine("Stream processing complete.")
