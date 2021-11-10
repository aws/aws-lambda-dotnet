using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace TestServerlessApp.IntegrationTests
{
    [Collection("Integration Tests")]
    public class Greeter
    {
        private readonly IntegrationTestContextFixture _fixture;

        public Greeter(IntegrationTestContextFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task SayHello_FromQuery_LogsToCloudWatch()
        {
            var response = await _fixture.HttpClient.GetAsync($"{_fixture.HttpApiUrlPrefix}/Greeter/SayHello?names=Alice&names=Bob");
            response.EnsureSuccessStatusCode();
            var lambdaFunctionName = _fixture.LambdaFunctions.FirstOrDefault(x => string.Equals(x.LogicalId, "GreeterSayHello"))?.Name;
            Assert.False(string.IsNullOrEmpty(lambdaFunctionName));
            var logGroupName = _fixture.CloudWatchHelper.GetLogGroupName(lambdaFunctionName);
            Assert.True(await _fixture.CloudWatchHelper.MessageExistsInRecentLogEventsAsync("Hello Alice", logGroupName, logGroupName));
            Assert.True(await _fixture.CloudWatchHelper.MessageExistsInRecentLogEventsAsync("Hello Bob", logGroupName, logGroupName));
        }

        [Fact]
        public async Task SayHelloAsync_FromHeader_LogsToCloudWatch()
        {
            var httpRequestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"{_fixture.HttpApiUrlPrefix}/Greeter/SayHelloAsync"),
                Headers = {{ "names", new List<string>{"Alice", "Bob"}}}
            };
            var response = _fixture.HttpClient.SendAsync(httpRequestMessage).Result;
            response.EnsureSuccessStatusCode();
            var lambdaFunctionName = _fixture.LambdaFunctions.FirstOrDefault(x => string.Equals(x.LogicalId, "GreeterSayHelloAsync"))?.Name;
            Assert.False(string.IsNullOrEmpty(lambdaFunctionName));
            var logGroupName = _fixture.CloudWatchHelper.GetLogGroupName(lambdaFunctionName);
            Assert.True(await _fixture.CloudWatchHelper.MessageExistsInRecentLogEventsAsync("Hello Alice, Bob", logGroupName, logGroupName));
        }
    }
}