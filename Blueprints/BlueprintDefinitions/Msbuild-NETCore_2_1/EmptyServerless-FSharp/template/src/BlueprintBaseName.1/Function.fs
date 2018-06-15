namespace BlueprintBaseName._1

open System.Net
open System.Collections.Generic

open Amazon.Lambda.Core
open Amazon.Lambda.APIGatewayEvents

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
()


type Functions() =

    /// <summary>
    /// A Lambda function to respond to HTTP Get methods from API Gateway
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    /// <returns>The list of blogs</returns>
    member _this.Get (request: APIGatewayProxyRequest) (context: ILambdaContext) =
        context.Logger.LogLine(sprintf "Request: %s" request.Path);

        let response = APIGatewayProxyResponse(
                        StatusCode = (int)HttpStatusCode.OK,
                        Body = "Hello AWS Serverless",
                        Headers = Dictionary<string, string>()
                        )
        response.Headers.Add("Content-Type", "text/plain")

        response
