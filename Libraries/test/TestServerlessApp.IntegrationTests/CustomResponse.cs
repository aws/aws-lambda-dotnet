using System.Net;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace TestServerlessApp.IntegrationTests
{
    [TestFixture]
    public class CustomResponse : IntegrationTestsSetup
    {
        [Test]
        [Retry(5)]
        public async Task OkResponseWithHeader_Returns200Status()
        {
            var response = await HttpClient.GetAsync($"{RestApiUrlPrefix}/okresponsewithheader/1");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("All Good"));
        }

        [Test]
        [Retry(5)]
        public async Task OkResponseWithHeader_ReturnsValidationErrors()
        {
            var response = await HttpClient.GetAsync($"{RestApiUrlPrefix}/okresponsewithheader/hello");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var content = await response.Content.ReadAsStringAsync();
            var errorJson = JObject.Parse(content);

            var expectedErrorMessage = "1 validation error(s) detected: Value hello at 'x' failed to satisfy constraint: Input string was not in a correct format.";
            Assert.That(errorJson["message"].ToString(), Is.EqualTo(expectedErrorMessage));
        }

        [Test]
        [Retry(5)]
        public async Task OkResponseWithCustomSerializer_Returns200Status()
        {
            var response = await HttpClient.GetAsync($"{HttpApiUrlPrefix}/okresponsewithcustomserializerasync/John/Doe");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var content = await response.Content.ReadAsStringAsync();
            var person = JObject.Parse(content);
            Assert.That(person["FIRST_NAME"].ToString(), Is.EqualTo("John"));
            Assert.That(person["LAST_NAME"].ToString(), Is.EqualTo("Doe"));
        }
    }
}
