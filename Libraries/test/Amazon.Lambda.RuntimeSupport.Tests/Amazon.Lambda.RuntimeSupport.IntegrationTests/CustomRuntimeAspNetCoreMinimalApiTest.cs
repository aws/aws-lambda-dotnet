using Amazon.IdentityManagement;
using Amazon.S3;
using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.RuntimeSupport.IntegrationTests.Helpers;
using NUnit.Framework;

namespace Amazon.Lambda.RuntimeSupport.IntegrationTests
{
    public class CustomRuntimeAspNetCoreMinimalApiTest : BaseCustomRuntimeTest
    {
        public CustomRuntimeAspNetCoreMinimalApiTest()
            : base("CustomRuntimeAspNetCoreMinimalApiTest-" + DateTime.Now.Ticks, "CustomRuntimeAspNetCoreMinimalApiTest.zip", @"CustomRuntimeAspNetCoreMinimalApiTest\bin\Release\net6.0\CustomRuntimeAspNetCoreMinimalApiTest.zip", "bootstrap")
        {
        }


#if SKIP_RUNTIME_SUPPORT_INTEG_TESTS
        [Test]
        [Ignore("Skipped intentionally by setting the SkipRuntimeSupportIntegTests build parameter.")]
#else
        [Test]
#endif
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

#if SKIP_RUNTIME_SUPPORT_INTEG_TESTS
        [Test]
        [Ignore("Skipped intentionally by setting the SkipRuntimeSupportIntegTests build parameter.")]
#else
        [Test]
#endif
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
            Assert.Equals(200, response.StatusCode);

            var apiGatewayResponse = System.Text.Json.JsonSerializer.Deserialize<APIGatewayHttpApiV2ProxyResponse>(response.Payload);
            Assert.Equals("application/json; charset=utf-8", apiGatewayResponse.Headers["Content-Type"]);
            Assert.That(apiGatewayResponse.Body, Does.Contain("temperatureC"));
        }

        private async Task InvokeLoggerTestController(IAmazonLambda lambdaClient)
        {
            var payload = File.ReadAllText("get-loggertest-request.json");
            var response = await InvokeFunctionAsync(lambdaClient, payload);
            Assert.Equals(200, response.StatusCode);

            var apiGatewayResponse = System.Text.Json.JsonSerializer.Deserialize<APIGatewayHttpApiV2ProxyResponse>(response.Payload);
            Assert.That(apiGatewayResponse.Body, Does.Contain("90000"));
        }
    }
}
