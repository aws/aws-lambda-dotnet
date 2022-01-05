using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace TestServerlessApp.IntegrationTests
{
    [Collection("Integration Tests")]
    public class SimpleCalculator
    {
        private readonly IntegrationTestContextFixture _fixture;

        public SimpleCalculator(IntegrationTestContextFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Add_FromQuery_ReturnsIntAsString()
        {
            var response = await _fixture.HttpClient.GetAsync($"{_fixture.RestApiUrlPrefix}/SimpleCalculator/Add?x=2&y=4");
            response.EnsureSuccessStatusCode();
            Assert.Equal("6", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Subtract_FromHeader_ReturnsIntAsString()
        {
            var httpRequestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"{_fixture.RestApiUrlPrefix}/SimpleCalculator/Subtract"),
                Headers = {{ "x", "10" }, {"y", "2"}}
            };
            var response = await _fixture.HttpClient.SendAsync(httpRequestMessage);
            response.EnsureSuccessStatusCode();
            Assert.Equal("8", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Multiply_FromPath_ReturnsIntAsString()
        {
            var response = await _fixture.HttpClient.GetAsync($"{_fixture.RestApiUrlPrefix}/SimpleCalculator/Multiply/2/10");
            response.EnsureSuccessStatusCode();
            Assert.Equal("20", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task DivideAsync_FromPath_ReturnsIntAsString()
        {
            var response = await _fixture.HttpClient.GetAsync($"{_fixture.RestApiUrlPrefix}/SimpleCalculator/DivideAsync/50/5");
            response.EnsureSuccessStatusCode();
            Assert.Equal("10", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Pi_NoPayload_ReturnsDoubleAsString()
        {
            var lambdaFunctionName = _fixture.LambdaFunctions.FirstOrDefault(x => string.Equals(x.LogicalId, "PI"))?.Name;
            Assert.False(string.IsNullOrEmpty(lambdaFunctionName));
            var invokeResponse = await _fixture.LambdaHelper.InvokeFunctionAsync(lambdaFunctionName);
            Assert.Equal(200, invokeResponse.StatusCode);
            var responsePayload = await new StreamReader(invokeResponse.Payload).ReadToEndAsync();
            Assert.Equal("3.141592653589793", responsePayload);
        }

        [Fact]
        public async Task Random_IntAsStringPayload_ReturnsRandomIntLessThanPayload()
        {
            var lambdaFunctionName = _fixture.LambdaFunctions.FirstOrDefault(x => string.Equals(x.LogicalId, "Random"))?.Name;
            Assert.False(string.IsNullOrEmpty(lambdaFunctionName));
            var invokeResponse = await _fixture.LambdaHelper.InvokeFunctionAsync(lambdaFunctionName, "1000");
            Assert.Equal(200, invokeResponse.StatusCode);
            var responsePayload = await new StreamReader(invokeResponse.Payload).ReadToEndAsync();
            Assert.True(int.TryParse(responsePayload, out var result));
            Assert.True(result < 1000);
        }

        [Fact]
        public async Task Randoms_JsonPayload_ReturnsListOfInt()
        {
            var lambdaFunctionName = _fixture.LambdaFunctions.FirstOrDefault(x => string.Equals(x.LogicalId, "Randoms"))?.Name;
            Assert.False(string.IsNullOrEmpty(lambdaFunctionName));
            var invokeResponse = await _fixture.LambdaHelper.InvokeFunctionAsync(lambdaFunctionName, "{\"count\": 5, \"maxValue\": 1000}");
            Assert.Equal(200, invokeResponse.StatusCode);
            var responsePayload = await new StreamReader(invokeResponse.Payload).ReadToEndAsync();
            var responseArray = responsePayload.Trim(new[] {'[', ']'}).Split(',').Select(int.Parse).ToList();
            Assert.Equal(5, responseArray.Count);
            Assert.True(responseArray.All(x => x < 1000));
        }
    }
}