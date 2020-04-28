namespace BlueprintBaseName._1


open Amazon.Lambda.Core
open Amazon.Lambda.APIGatewayEvents

open System.Net


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>)>]
()


type Functions() =
    /// <summary>
    /// A Lambda function to respond to HTTP Get methods from API Gateway
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    member __.Get (request: APIGatewayProxyRequest) (context: ILambdaContext) =
        sprintf "Request: %s" request.Path
        |> context.Logger.LogLine

        APIGatewayProxyResponse(
            StatusCode = int HttpStatusCode.OK,
            Body = "Hello AWS Serverless",
            Headers = dict [ ("Content-Type", "text/plain") ]
        )
