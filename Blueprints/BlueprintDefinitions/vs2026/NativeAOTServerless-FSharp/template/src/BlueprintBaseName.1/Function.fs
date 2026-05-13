namespace BlueprintBaseName._1

open Amazon.Lambda.Core
open Amazon.Lambda.RuntimeSupport
open Amazon.Lambda.Serialization.SystemTextJson
open Amazon.Lambda.APIGatewayEvents

open System
open System.Net

module Function =


    /// <summary>
    /// A Lambda function to respond to HTTP Get methods from API Gateway.
    ///
    /// To use this handler to respond to an AWS event, reference the appropriate package from 
    /// https://github.com/aws/aws-lambda-dotnet#events
    /// and change the string input parameter to the desired event type.
    ///
    // When using Native AOT extra testing with the deployed Lambda functions is required to ensure
    // the libraries used in the Lambda function work correctly with Native AOT. If a runtime 
    // error occurs about missing types or methods the most likely solution will be to remove references to trim-unsafe 
    // code or configure trimming options. This sample defaults to partial TrimMode because currently the AWS 
    // SDK for .NET does not support trimming. This will result in a larger executable size, and still does not 
    // guarantee runtime trimming errors won't be hit. 

    /// </summary>
    /// <param name="input">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    let GetFunctionHandler (request: APIGatewayProxyRequest) (context: ILambdaContext) =
        sprintf "Request: %s" request.Path
        |> context.Logger.LogInformation

        APIGatewayProxyResponse(
            StatusCode = int HttpStatusCode.OK,
            Body = "Hello AWS Serverless",
            Headers = dict [ ("Content-Type", "text/plain") ]
        )


    /// <summary>
    /// The main entry point for the Lambda function. The main function is called once during the Lambda init phase. It
    /// initializes the .NET Lambda runtime client passing in the function handler to invoke for each Lambda event and
    /// the JSON serializer to use for converting Lambda JSON format to the .NET types. 
    ///
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    [<EntryPoint>]
    let main _args =
    
        let handler = Func<APIGatewayProxyRequest, ILambdaContext, APIGatewayProxyResponse>(GetFunctionHandler)
        use bootstrap = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                            .Build()
   
        bootstrap.RunAsync().GetAwaiter().GetResult()
        0