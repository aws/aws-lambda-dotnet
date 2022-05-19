using Amazon.Lambda.Annotations.Testing;
using Xunit;

namespace TestServerlessApp.InMemory.IntegrationTests
{
    public class SimpleCalculator
    {
        private readonly HttpClient _httpClient;

        public SimpleCalculator()
        {
            _httpClient = new WebApplicationFactory(typeof(Startup).Assembly).CreateHttpClient();
        }

        [Fact]
        public async Task Add_FromQuery_ReturnsIntAsString()
        {
            var response = await _httpClient.GetAsync("/SimpleCalculator/Add?x=2&y=4");
            response.EnsureSuccessStatusCode();
            Assert.Equal("6", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Subtract_FromHeader_ReturnsIntAsString()
        {
            var httpRequestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("/SimpleCalculator/Subtract", UriKind.Relative),
                Headers = { { "x", "10" }, { "y", "2" } }
            };
            var response = await _httpClient.SendAsync(httpRequestMessage);
            response.EnsureSuccessStatusCode();
            Assert.Equal("8", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Multiply_FromPath_ReturnsIntAsString()
        {
            var response = await _httpClient.GetAsync("/SimpleCalculator/Multiply/2/10");
            response.EnsureSuccessStatusCode();
            Assert.Equal("20", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task DivideAsync_FromPath_ReturnsIntAsString()
        {
            var response = await _httpClient.GetAsync("/SimpleCalculator/DivideAsync/50/5");
            response.EnsureSuccessStatusCode();
            Assert.Equal("10", await response.Content.ReadAsStringAsync());
        }
    }
}