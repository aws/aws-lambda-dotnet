using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.Lambda.TestUtilities;

using Newtonsoft.Json;

using TestWebApp;

using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Test
{
    public class TestApplicationLoadBalancerCalls
    {
        [Fact]
        public async Task TestGetAllValues()
        {
            var context = new TestLambdaContext();

            var response = await this.InvokeApplicationLoadBalancerRequest(context, "values-get-all-alb-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("[\"value1\",\"value2\"]", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("application/json; charset=utf-8", response.Headers["Content-Type"]);

            Assert.Contains("OnStarting Called", ((TestLambdaLogger)context.Logger).Buffer.ToString());
        }

        [Fact]
        public async Task TestGetQueryStringValue()
        {
            var response = await this.InvokeApplicationLoadBalancerRequest("values-get-querystring-alb-request.json");

            Assert.Equal("Lewis, Meriwether", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.Headers["Content-Type"]);
        }

        [Fact]
        public async Task TestGetNoQueryStringAlb()
        {
            var response = await this.InvokeApplicationLoadBalancerRequest("values-get-no-querystring-alb-request.json");

            Assert.Equal(string.Empty, response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.Headers["Content-Type"]);
        }

        [Fact]
        public async Task TestGetNoQueryStringAlbMv()
        {
            var response = await this.InvokeApplicationLoadBalancerRequest("values-get-no-querystring-alb-mv-request.json");
            Assert.Equal(string.Empty, response.Body);
        }

        [Fact]
        public async Task TestGetEncodingQueryStringAlb()
        {
            var response = await this.InvokeApplicationLoadBalancerRequest("values-get-querystring-alb-encoding-request.json");
            var results = JsonConvert.DeserializeObject<TestWebApp.Controllers.RawQueryStringController.Results>(response.Body);
            Assert.Equal("http://www.gooogle.com", results.Url);
            Assert.Equal(DateTimeOffset.Parse("2019-03-12T16:06:06.549817+00:00"), results.TestDateTimeOffset);

            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("application/json; charset=utf-8", response.Headers["Content-Type"]);
        }

        [Fact]
        public async Task TestGetEncodingQueryStringAlbMv()
        {
            var response = await this.InvokeApplicationLoadBalancerRequest("values-get-querystring-alb-mv-encoding-request.json");
            var results = JsonConvert.DeserializeObject<TestWebApp.Controllers.RawQueryStringController.Results>(response.Body);
            Assert.Equal("http://www.gooogle.com", results.Url);
            Assert.Equal(DateTimeOffset.Parse("2019-03-12T16:06:06.549817+00:00"), results.TestDateTimeOffset);

            Assert.True(response.MultiValueHeaders.ContainsKey("Content-Type"));
            Assert.Equal("application/json; charset=utf-8", response.MultiValueHeaders["Content-Type"][0]);
        }

        [Fact]
        public async Task TestGetQueryStringValueMV()
        {
            var response = await this.InvokeApplicationLoadBalancerRequest("values-get-querystring-alb-mv-request.json");

            Assert.Equal("value1,value2", response.Body);
            Assert.True(response.MultiValueHeaders.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.MultiValueHeaders["Content-Type"].FirstOrDefault());
        }

        [Fact]
        public async Task TestPutWithBody()
        {
            var response = await this.InvokeApplicationLoadBalancerRequest("values-put-withbody-alb-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("Agent, Smith", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.Headers["Content-Type"]);
        }

        [Fact]
        public async Task TestPutWithBodyMV()
        {
            var response = await this.InvokeApplicationLoadBalancerRequest("values-put-withbody-alb-mv-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("Agent, Smith", response.Body);
            Assert.True(response.MultiValueHeaders.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.MultiValueHeaders["Content-Type"][0]);
        }

        [Fact]
        public async Task TestGetSingleValue()
        {
            var response = await this.InvokeApplicationLoadBalancerRequest("values-get-single-alb-request.json");

            Assert.Equal("value=5", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.Headers["Content-Type"]);
        }

        [Fact]
        public async Task TestGetBinaryContent()
        {
            var response = await this.InvokeApplicationLoadBalancerRequest("values-get-binary-alb-request.json");

            Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);

            string contentType;
            Assert.True(response.Headers.TryGetValue("Content-Type", out contentType),
                    "Content-Type response header exists");
            Assert.Equal("application/octet-stream", contentType);
            Assert.NotNull(response.Body);
            Assert.True(response.Body.Length > 0,
                    "Body content is not empty");

            Assert.True(response.IsBase64Encoded, "Response IsBase64Encoded");

            // Compute a 256-byte array, with values 0-255
            var binExpected = new byte[byte.MaxValue].Select((val, index) => (byte)index).ToArray();
            var binActual = Convert.FromBase64String(response.Body);
            Assert.Equal(binExpected, binActual);
        }

        [Fact]
        public async Task TestPutBinaryContent()
        {
            var response = await this.InvokeApplicationLoadBalancerRequest("values-put-binary-alb-request.json");

            Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);

            Assert.NotNull(response.Body);
            Assert.Equal("Greetings, programs!", response.Body);

            Assert.False(response.IsBase64Encoded, "Response IsBase64Encoded");
        }

        [Fact]
        public async Task TestHealthCheck()
        {
            var response = await this.InvokeApplicationLoadBalancerRequest("alb-healthcheck.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("[\"value1\",\"value2\"]", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("application/json; charset=utf-8", response.Headers["Content-Type"]);
        }

        [Fact]
        public async Task TestContentLengthWithContent()
        {
            var response = await this.InvokeApplicationLoadBalancerRequest("check-content-length-withcontent-alb.json");
            Assert.Equal("Request content length: 17", response.Body.Trim());
        }

        [Fact]
        public async Task TestContentLengthNoContent()
        {
            var response = await this.InvokeApplicationLoadBalancerRequest("check-content-length-nocontent-alb.json");
            Assert.Equal("Request content length: 0", response.Body.Trim());
        }

        [Fact]
        public async Task TestGetCompressResponse()
        {
            var context = new TestLambdaContext();

            var response = await this.InvokeApplicationLoadBalancerRequest(context, "compressresponse-get-alb-request.json");

            Assert.Equal(200, response.StatusCode);

            var bytes = Convert.FromBase64String(response.Body);
            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(msi, CompressionMode.Decompress))
                {
                    gs.CopyTo(mso);

                }
                var body = Encoding.UTF8.GetString(mso.ToArray());
                Assert.Equal("[\"value1\",\"value2\"]", body);
            }

            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("application/json-compress", response.Headers["Content-Type"]);

            Assert.True(response.Headers.ContainsKey("Content-Encoding"));
            Assert.Equal("gzip", response.Headers["Content-Encoding"]);

            Assert.Contains("OnStarting Called", ((TestLambdaLogger)context.Logger).Buffer.ToString());
        }

        private async Task<ApplicationLoadBalancerResponse> InvokeApplicationLoadBalancerRequest(string fileName)
        {
            return await InvokeApplicationLoadBalancerRequest(new TestLambdaContext(), fileName);
        }

        private async Task<ApplicationLoadBalancerResponse> InvokeApplicationLoadBalancerRequest(TestLambdaContext context, string fileName)
        {
            var lambdaFunction = new ALBLambdaFunction();
            var filePath = Path.Combine(Path.GetDirectoryName(this.GetType().GetTypeInfo().Assembly.Location), fileName);
            var requestStr = File.ReadAllText(filePath);
            var request = JsonConvert.DeserializeObject<ApplicationLoadBalancerRequest>(requestStr);
            return await lambdaFunction.FunctionHandlerAsync(request, context);
        }

    }
}
