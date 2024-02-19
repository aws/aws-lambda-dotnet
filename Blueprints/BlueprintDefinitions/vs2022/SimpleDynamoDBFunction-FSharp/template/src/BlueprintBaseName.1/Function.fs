﻿namespace BlueprintBaseName._1


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
    /// <param name="dynamoEvent">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    member __.FunctionHandler (dynamoEvent: DynamoDBEvent) (context: ILambdaContext) =
        sprintf "Beginning to process %i records..." dynamoEvent.Records.Count
        |> context.Logger.LogInformation

        let processRecord (record: DynamoDBEvent.DynamodbStreamRecord) =
            context.Logger.LogInformation(sprintf "Event ID: %s" record.EventID)
            context.Logger.LogInformation(sprintf "Event Name: %s" record.EventName)
            // TODO: Add business logic processing the record.Dynamodb object.

        dynamoEvent.Records
        |> Seq.iter processRecord

        context.Logger.LogInformation("Stream processing complete.")
