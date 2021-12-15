namespace BlueprintBaseName._1


open Microsoft.AspNetCore.Hosting


type LambdaEntryPoint() =

// The base class must be set to match the AWS service invoking the Lambda function. If not Amazon.Lambda.AspNetCoreServer
// will fail to convert the incoming request correctly into a valid ASP.NET Core request.
//
// API Gateway REST API                         -> Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction
// API Gateway HTTP API payload version 1.0     -> Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction
// API Gateway HTTP API payload version 2.0     -> Amazon.Lambda.AspNetCoreServer.APIGatewayHttpApiV2ProxyFunction
// Application Load Balancer                    -> Amazon.Lambda.AspNetCoreServer.ApplicationLoadBalancerFunction
// 
// Note: When using the AWS::Serverless::Function resource with an event type of "HttpApi" then payload version 2.0
// will be the default and you must make Amazon.Lambda.AspNetCoreServer.APIGatewayHttpApiV2ProxyFunction the base class.

    inherit Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction()

    override this.Init(builder: IWebHostBuilder) =
        builder
            .UseStartup<Startup>()
        |> ignore
