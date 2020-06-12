using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.APIGatewayEvents;

using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

using Amazon;
using Amazon.S3;
using Amazon.S3.Util;
using Amazon.S3.Model;

using BlueprintBaseName._1;


namespace BlueprintBaseName._1.Tests
{
    public class S3ProxyControllerTests : IDisposable
    {
        string BucketName { get; set; }
        IAmazonS3 S3Client { get; set; }

        IConfigurationRoot Configuration { get; set; }


        public S3ProxyControllerTests()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            this.Configuration = builder.Build();

            // Use the region and possible profile specified in the appsettings.json file to construct an Amazon S3 service client.
            this.S3Client = Configuration.GetAWSOptions().CreateServiceClient<IAmazonS3>();

            // Create a bucket used for the test which will be deleted along with any data in the bucket once the test is complete.
            this.BucketName = "lambda-S3ProxyControllerTests-".ToLower() + DateTime.Now.Ticks;
            this.S3Client.PutBucketAsync(this.BucketName).Wait();
        }

        [Fact]
        public async Task TestSuccessWorkFlow()
        {
            var lambdaFunction = new LambdaEntryPoint();
            Startup.Configuration[Startup.AppS3BucketKey] = this.BucketName;

            // Use sample API Gateway request that uploads an object with object key "foo.txt" and content of "Hello World".
            var requestStr = File.ReadAllText("./SampleRequests/S3ProxyController-Put.json");
            var request = JsonConvert.DeserializeObject<APIGatewayProxyRequest>(requestStr);
            var context = new TestLambdaContext();
            var response = await lambdaFunction.FunctionHandlerAsync(request, context);

            Assert.Equal(200, response.StatusCode);

            // Get with no object key in the resource path does an object list call
            requestStr = File.ReadAllText("./SampleRequests/S3ProxyController-Get.json");
            request = JsonConvert.DeserializeObject<APIGatewayProxyRequest>(requestStr);
            context = new TestLambdaContext();
            response = await lambdaFunction.FunctionHandlerAsync(request, context);

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("text/json", response.MultiValueHeaders["Content-Type"][0]);
			Assert.Contains("foo.txt", response.Body);

            // Return the content of the new s3 object foo.txt
            requestStr = File.ReadAllText("./SampleRequests/S3ProxyController-GetByKey.json");
            request = JsonConvert.DeserializeObject<APIGatewayProxyRequest>(requestStr);
            context = new TestLambdaContext();
            response = await lambdaFunction.FunctionHandlerAsync(request, context);

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("text/plain", response.MultiValueHeaders["Content-Type"][0]);
            Assert.Equal("Hello World", response.Body);

            // Delete the object
            requestStr = File.ReadAllText("./SampleRequests/S3ProxyController-Delete.json");
            request = JsonConvert.DeserializeObject<APIGatewayProxyRequest>(requestStr);
            context = new TestLambdaContext();
            response = await lambdaFunction.FunctionHandlerAsync(request, context);

            Assert.Equal(200, response.StatusCode);

            // Make sure the object was deleted
            requestStr = File.ReadAllText("./SampleRequests/S3ProxyController-GetByKey.json");
            request = JsonConvert.DeserializeObject<APIGatewayProxyRequest>(requestStr);
            context = new TestLambdaContext();
            response = await lambdaFunction.FunctionHandlerAsync(request, context);

            Assert.Equal(404, response.StatusCode);
        }



        private bool disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    AmazonS3Util.DeleteS3BucketWithObjectsAsync(this.S3Client, BucketName).Wait();
                    this.S3Client.Dispose();
                }

                disposedValue = true;
            }
        }


        public void Dispose()
        {
            Dispose(true);
        }
    }
}
