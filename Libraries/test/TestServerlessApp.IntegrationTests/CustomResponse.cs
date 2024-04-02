using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace TestServerlessApp.IntegrationTests
{
    [Collection("Integration Tests")]
    public class CustomResponse
    {
        private readonly IntegrationTestContextFixture _fixture;

        public CustomResponse(IntegrationTestContextFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task OkResponseWithHeader_Returns200Status()
        {
            var response = await _fixture.HttpClient.GetAsync($"{_fixture.RestApiUrlPrefix}/okresponsewithheader/1");
            response.EnsureSuccessStatusCode();
            Assert.Equal("All Good", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task OkResponseWithHeader_ReturnsValidationErrors()
        {
            var response = await _fixture.HttpClient.GetAsync($"{_fixture.RestApiUrlPrefix}/okresponsewithheader/hello");
            Assert.Equal(400, (int)response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            var errorJson = JObject.Parse(content);

            var expectedErrorMessage = "1 validation error(s) detected: Value hello at 'x' failed to satisfy constraint: Input string was not in a correct format.";
            Assert.Equal(expectedErrorMessage, errorJson["message"]);
        }
    }
}
