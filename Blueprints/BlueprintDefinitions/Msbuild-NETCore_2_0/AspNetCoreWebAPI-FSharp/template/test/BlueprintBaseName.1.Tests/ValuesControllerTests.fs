namespace BlueprintBaseName.1.Tests

open Xunit
open Amazon.Lambda.TestUtilities
open Amazon.Lambda.APIGatewayEvents
open System.IO
open Newtonsoft.Json
open BlueprintBaseName.1

module ValuesControllerTests =

    [<Fact>]
    let ``Request HTTP Get at /``() = async {
        let lambdaFunction = new LambdaEntryPoint()
        let requestStr = File.ReadAllText("./SampleRequests/ValuesController-Get.json")
        let request = JsonConvert.DeserializeObject<APIGatewayProxyRequest>(requestStr)
        let context = new TestLambdaContext()
        let! response = lambdaFunction.FunctionHandlerAsync(request, context) |> Async.AwaitTask

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("[\"value1\",\"value2\"]", response.Body);
        Assert.True(response.Headers.ContainsKey("Content-Type"));
        Assert.Equal("application/json; charset=utf-8", response.Headers.Item("Content-Type"));
    }