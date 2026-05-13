using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
            var response = await GetWithRetryAsync($"{_fixture.RestApiUrlPrefix}/okresponsewithheader/1");
            response.EnsureSuccessStatusCode();
            Assert.Equal("All Good", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task OkResponseWithHeader_ReturnsValidationErrors()
        {
            var response = await GetWithRetryAsync($"{_fixture.RestApiUrlPrefix}/okresponsewithheader/hello");
            Assert.Equal(400, (int)response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            var errorJson = JObject.Parse(content);

            var expectedErrorMessage = "1 validation error(s) detected: Value hello at 'x' failed to satisfy constraint: Input string was not in a correct format.";
            Assert.Equal(expectedErrorMessage, errorJson["message"]);
        }

        [Fact]
        public async Task OkResponseWithCustomSerializer_Returns200Status()
        {
            var response = await GetWithRetryAsync($"{_fixture.HttpApiUrlPrefix}/okresponsewithcustomserializerasync/John/Doe");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var person = JObject.Parse(content);
            Assert.Equal("John", person["FIRST_NAME"]);
            Assert.Equal("Doe", person["LAST_NAME"]);
        }

        private async Task<HttpResponseMessage> GetWithRetryAsync(string path)
        {
            int MAX_ATTEMPTS = 10;
            HttpResponseMessage response = null;
            for (var retryAttempt = 0; retryAttempt < MAX_ATTEMPTS; retryAttempt++)
            {
                await Task.Delay(retryAttempt * 1000);
                try
                {
                    response = await _fixture.HttpClient.GetAsync(path);

                    // No tests are coded to return 403 Forbidden. If this is returned it is likely
                    // an eventual consistency issue.
                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        continue;

                    break;
                }
                catch
                {
                    if (retryAttempt + 1 == MAX_ATTEMPTS)
                    {
                        throw;
                    }
                }
            }

            return response;
        }
    }
}
