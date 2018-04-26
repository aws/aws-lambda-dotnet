namespace BlueprintBaseName._1.Tests

open Xunit
open Amazon.Lambda.TestUtilities
open Amazon.Lambda.APIGatewayEvents
open System.IO
open Newtonsoft.Json
open Setup


module HttpHandlersTests =

    [<Fact>]
    let ``Request HTTP Get at /``() = async {
        let lambdaFunction = new LambdaEntryPoint()
        let requestStr = File.ReadAllText("./SampleRequests/GetAtRoot.json")
        let request = JsonConvert.DeserializeObject<APIGatewayProxyRequest>(requestStr)
        let context = new TestLambdaContext()
        let! response = lambdaFunction.FunctionHandlerAsync(request, context) |> Async.AwaitTask

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Serverless Giraffe Web API", response.Body);
        Assert.True(response.Headers.ContainsKey("Content-Type"));
        Assert.Equal("text/plain", response.Headers.Item("Content-Type"));
    }    

        [<Fact>]
    let ``Request HTTP Get at /array``() = async {
        let lambdaFunction = new LambdaEntryPoint()
        let requestStr = File.ReadAllText("./SampleRequests/GetAtArray.json")
        let request = JsonConvert.DeserializeObject<APIGatewayProxyRequest>(requestStr)
        let context = new TestLambdaContext()
        let! response = lambdaFunction.FunctionHandlerAsync(request, context) |> Async.AwaitTask

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("value1, value2", response.Body);
        Assert.True(response.Headers.ContainsKey("Content-Type"));
        Assert.Equal("text/plain", response.Headers.Item("Content-Type"));
    }    

        [<Fact>]
    let ``Request HTTP Get at /array/5``() = async {
        let lambdaFunction = new LambdaEntryPoint()
        let requestStr = File.ReadAllText("./SampleRequests/GetAtArrayWithValue.json")
        let request = JsonConvert.DeserializeObject<APIGatewayProxyRequest>(requestStr)
        let context = new TestLambdaContext()
        let! response = lambdaFunction.FunctionHandlerAsync(request, context) |> Async.AwaitTask

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("value1, value2, value3, value4, value5", response.Body);
        Assert.True(response.Headers.ContainsKey("Content-Type"));
        Assert.Equal("text/plain", response.Headers.Item("Content-Type"));
    }    