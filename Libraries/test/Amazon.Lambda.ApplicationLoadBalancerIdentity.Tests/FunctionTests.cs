namespace Amazon.Lambda.ApplicationLoadBalancerIdentity.Tests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Primitives;
    using Microsoft.IdentityModel.JsonWebTokens;
    using Moq;
    using Moq.Protected;
    using Xunit;

    public class FunctionTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData(ushort.MaxValue)]
        public async Task ValidationTest(ushort? cacheSize)
        {
            await TestConstants.TestLock.WaitAsync();
            try
            {
                await RunTest(cacheSize);
            }
            finally
            {
                ALBIdentityMiddleware.InternalHttpClient?.Dispose();
                TestConstants.TestLock.Release();
            }
        }

        private static async Task RunTest(ushort? cacheSize)
        {
            var opts = new ALBIdentityMiddlewareOptions
            {
                VerifyTokenSignature = true,
                MaxCacheSizeMB = cacheSize,
                ValidateTokenLifetime = false
            };

            Environment.SetEnvironmentVariable(ALBIdentityMiddleware.AWSRegionEnvironmentVariable, "us-west-2");

            var jwt = new JsonWebToken(TestConstants.TokenData);
            var expectedUri = string.Format(ALBIdentityMiddleware.ALBPublicKeyUrlFormatString, "us-west-2", jwt.Kid);

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(new HttpResponseMessage
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent(TestConstants.PublicKey),
               })
               .Verifiable();

            var rd = new RequestDelegate((_) => Task.CompletedTask);

            ALBIdentityMiddleware.InternalHttpClient = new HttpClient(handlerMock.Object);
            var mw = new ALBIdentityMiddleware(rd, null, opts);

            var testContext = new DefaultHttpContext();
            testContext.Request.Headers.Add(ALBIdentityMiddleware.OidcDataHeader, new StringValues(TestConstants.TokenData));

            await mw.Invoke(testContext);

            Assert.NotNull(testContext.User);
            if (testContext.Response.StatusCode != 200)
            {
                using (var sr = new StreamReader(testContext.Response.Body))
                {
                    throw new Exception(sr.ReadToEnd());
                }
            }

            Assert.Equal(200, testContext.Response.StatusCode);
            Assert.Equal("yancej@amazon.com", testContext.User.Identity.Name);
            Assert.Equal("alboidc", testContext.User.FindFirst("aud").Value);

            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Get  // we expected a GET request
                  && req.RequestUri.ToString() == expectedUri // to this uri
               ),
               ItExpr.IsAny<CancellationToken>()
            );
        }
    }
}
