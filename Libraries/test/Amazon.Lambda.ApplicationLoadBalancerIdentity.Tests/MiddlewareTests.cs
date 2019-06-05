namespace Amazon.Lambda.ApplicationLoadBalancerIdentity.Tests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.IdentityModel.JsonWebTokens;
    using Moq;
    using Moq.Protected;
    using Xunit;

    public class MiddlewareTests : IClassFixture<CustomWebAppFactory<TestStartup>>
    {
        private readonly CustomWebAppFactory<TestStartup> fixture;

        public MiddlewareTests(CustomWebAppFactory<TestStartup> fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        public async Task MiddlewareTest()
        {
            await TestConstants.TestLock.WaitAsync();
            try
            {
                await this.RunTest();
            }
            finally
            {
                ALBIdentityMiddleware.InternalHttpClient?.Dispose();
                TestConstants.TestLock.Release();
            }
        }

        private async Task RunTest()
        {
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

            ALBIdentityMiddleware.InternalHttpClient = new HttpClient(handlerMock.Object);

            using (var client = this.fixture.CreateDefaultClient())
            {
                await SendRequest(TestConstants.TokenData, client);

                // Send request again, so that the mock can verify the cache is working
                await SendRequest(TestConstants.TokenData, client);
            }

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

        private static async Task SendRequest(string tokenData, HttpClient client)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Get, "/"))
            {
                req.Headers.TryAddWithoutValidation(ALBIdentityMiddleware.OidcDataHeader, tokenData);

                var resp = await client.SendAsync(req);
                if (resp.StatusCode != HttpStatusCode.OK)
                    throw new Exception(await resp.Content.ReadAsStringAsync());

                var user = await resp.Content.ReadAsStringAsync();
                Assert.Equal("yancej@amazon.com", user);
            }
        }
    }
}
