using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TestServerlessApp.IntegrationTests
{
    [Collection("Integration Tests")]
    public class ComplexCalculator
    {
        private readonly IntegrationTestContextFixture _fixture;

        public ComplexCalculator(IntegrationTestContextFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Add_FromBodyAsString_ReturnsJson()
        {
            var response = await _fixture.HttpClient.PostAsync($"{_fixture.HttpApiUrlPrefix}/ComplexCalculator/Add", new StringContent("1,2;3,4"));
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
            var response = await _fixture.HttpClient.PostAsync($"{_fixture.HttpApiUrlPrefix}/ComplexCalculator/Subtract", data);
            response.EnsureSuccessStatusCode();
            var responseJson = JObject.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(-2, responseJson["Item1"]);
            Assert.Equal(-2, responseJson["Item2"]);
        }
    }
}