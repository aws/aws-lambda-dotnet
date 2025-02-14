namespace BlueprintBaseName._1.Tests


open Xunit
open Amazon.Lambda.TestUtilities
open Amazon.Lambda.APIGatewayEvents

open BlueprintBaseName._1


module FunctionsTest =
    [<Fact>]
    let ``Call HTTP GET on Root``() =
        let functions = Functions()
        let context = TestLambdaContext()
        let request = APIGatewayProxyRequest()
        let response = functions.Get request context

        Assert.Equal(200, response.StatusCode)
        Assert.Equal("Hello AWS Serverless", response.Body)

