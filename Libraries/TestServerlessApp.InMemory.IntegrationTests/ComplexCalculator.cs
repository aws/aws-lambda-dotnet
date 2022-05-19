using Amazon.Lambda.Annotations.Testing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using Xunit;

namespace TestServerlessApp.InMemory.IntegrationTests
{
    public class ComplexCalculator
    {
        private readonly HttpClient _httpClient;

        public ComplexCalculator()
        {
            _httpClient = new WebApplicationFactory(typeof(Startup).Assembly).CreateHttpClient();
        }

        [Fact]
        public async Task Add_FromBodyAsString_ReturnsJson()
        {
            var response = await _httpClient.PostAsync("/ComplexCalculator/Add", new StringContent("1,2;3,4"));
            response.EnsureSuccessStatusCode();
            var responseJson = JObject.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(4, responseJson["Item1"]);
            Assert.Equal(6, responseJson["Item2"]);
        }

        [Fact]
        public async Task Subtract_FromBodyAsList_ReturnsJson()
        {
            var json = JsonConvert.SerializeObject(new[,] { { 1, 2 }, { 3, 4 } });
            var data = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/ComplexCalculator/Subtract", data);
            response.EnsureSuccessStatusCode();
            var responseJson = JObject.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(-2, responseJson["Item1"]);
            Assert.Equal(-2, responseJson["Item2"]);
        }
    }
}