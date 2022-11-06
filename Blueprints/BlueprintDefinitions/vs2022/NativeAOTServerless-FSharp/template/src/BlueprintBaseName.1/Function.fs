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
    /// When using Native AOT, libraries used with your Lambda function might not be compatible with trimming that
    /// happens as part of the Native AOT compilation. If you find when testing your Native AOT Lambda function that 
    /// you get runtime errors about missing types, methods or constructors then add the assembly that contains the
    /// types into the rd.xml file. This will tell the Native AOT compiler to not trim those assemblies. Currently the 
    /// AWS SDK for .NET does not support trimming and when used should be added to the rd.xml file.    
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
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
    /// F# uses the DefaultLambdaJsonSerializer which uses reflection to convert the JSON events 
    /// and responses to .NET types. The Assembly name that contains the .NET types to serialize with must be added
    /// to the rd.xml file to avoid the Native AOT compiler from trimming out the types used by reflection. For
    /// example in the starting code the Amazon.Lambda.APIGatewayEvents assembly which defined the APIGatewayProxyRequest
    /// and APIGatewayProxyResponse types is added to the rd.xml file.
    /// </summary>
    /// <param name="args"></param>
    [<EntryPoint>]
    let main _args =
    
        let handler = Func<APIGatewayProxyRequest, ILambdaContext, APIGatewayProxyResponse>(GetFunctionHandler)
        use bootstrap = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                            .Build()
   
        bootstrap.RunAsync().GetAwaiter().GetResult()
        0