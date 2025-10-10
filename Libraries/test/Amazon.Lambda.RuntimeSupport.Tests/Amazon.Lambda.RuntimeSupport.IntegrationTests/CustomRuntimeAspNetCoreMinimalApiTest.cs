using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Lambda.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Amazon.Lambda.APIGatewayEvents;
using System.Text.Json;


namespace Amazon.Lambda.RuntimeSupport.IntegrationTests
{
    [Collection("Integration Tests")]
    public class CustomRuntimeAspNetCoreMinimalApiTest : BaseCustomRuntimeTest
    {
        public CustomRuntimeAspNetCoreMinimalApiTest(IntegrationTestFixture fixture)
            : base(fixture, "CustomRuntimeAspNetCoreMinimalApiTest-" + DateTime.Now.Ticks, "CustomRuntimeAspNetCoreMinimalApiTest.zip", @"CustomRuntimeAspNetCoreMinimalApiTest\bin\Release\net8.0\CustomRuntimeAspNetCoreMinimalApiTest.zip", "bootstrap")
        {
        }


        [Fact]
        public async Task TestMinimalApi()
        {
            // run all test cases in one test to ensure they run serially
            using (var lambdaClient = new AmazonLambdaClient(TestRegion))
            using (var s3Client = new AmazonS3Client(TestRegion))
            using (var iamClient = new AmazonIdentityManagementServiceClient(TestRegion))
            {
                var roleAlreadyExisted = false;

                try
                {
                    roleAlreadyExisted = await PrepareTestResources(s3Client, lambdaClient, iamClient);
                    await InvokeSuccessToWeatherForecastController(lambdaClient);
                }
                catch (NoDeploymentPackageFoundException)
                {
#if DEBUG
                    // The CodePipeline for this project doesn't currently build the deployment in the stage that runs 
                    // this test. For now ignore this test in release mode if the deployment package can't be found.
                    throw;
#endif
                }
                finally
                {
                    await CleanUpTestResources(s3Client, lambdaClient, iamClient, roleAlreadyExisted);
                }
            }
        }

        [Fact]
        public async Task TestThreadingLogging()
        {
            // run all test cases in one test to ensure they run serially
            using (var lambdaClient = new AmazonLambdaClient(TestRegion))
            using (var s3Client = new AmazonS3Client(TestRegion))
            using (var iamClient = new AmazonIdentityManagementServiceClient(TestRegion))
            {
                var roleAlreadyExisted = false;

                try
                {
                    roleAlreadyExisted = await PrepareTestResources(s3Client, lambdaClient, iamClient);
                    await InvokeLoggerTestController(lambdaClient);
                }
                catch (NoDeploymentPackageFoundException)
                {
#if DEBUG
                    // The CodePipeline for this project doesn't currently build the deployment in the stage that runs 
                    // this test. For now ignore this test in release mode if the deployment package can't be found.
                    throw;
#endif
                }
                finally
                {
                    await CleanUpTestResources(s3Client, lambdaClient, iamClient, roleAlreadyExisted);
                }
            }
        }

        private async Task InvokeSuccessToWeatherForecastController(IAmazonLambda lambdaClient)
        {
            var payload = File.ReadAllText("get-weatherforecast-request.json");
            var response = await InvokeFunctionAsync(lambdaClient, payload);
            Assert.Equal(200, response.StatusCode);

            var apiGatewayResponse = System.Text.Json.JsonSerializer.Deserialize<APIGatewayHttpApiV2ProxyResponse>(response.Payload);
            Assert.Equal("application/json; charset=utf-8", apiGatewayResponse.Headers["Content-Type"]);
            Assert.Contains("temperatureC", apiGatewayResponse.Body);
        }

        private async Task InvokeLoggerTestController(IAmazonLambda lambdaClient)
        {
            var payload = File.ReadAllText("get-loggertest-request.json");
            var response = await InvokeFunctionAsync(lambdaClient, payload);
            Assert.Equal(200, response.StatusCode);

            var apiGatewayResponse = System.Text.Json.JsonSerializer.Deserialize<APIGatewayHttpApiV2ProxyResponse>(response.Payload);
            Assert.Contains("90000", apiGatewayResponse.Body);
        }
    }
}
