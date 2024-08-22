using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace TestServerlessApp.IntegrationTests
{
    [TestFixture]
    public class CustomResponse : IntegrationTestsSetup
    {
        [Test]
        public async Task OkResponseWithHeader_Returns200Status()
        {
            var response = await HttpClient.GetAsync($"{RestApiUrlPrefix}/okresponsewithheader/1");
            response.EnsureSuccessStatusCode();
            Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("All Good"));
        }

        [Test]
        public async Task OkResponseWithHeader_ReturnsValidationErrors()
        {
            var response = await HttpClient.GetAsync($"{RestApiUrlPrefix}/okresponsewithheader/hello");
            Assert.That((int)response.StatusCode, Is.EqualTo(400));
            var content = await response.Content.ReadAsStringAsync();
            var errorJson = JObject.Parse(content);

            var expectedErrorMessage = "1 validation error(s) detected: Value hello at 'x' failed to satisfy constraint: Input string was not in a correct format.";
            Assert.That(errorJson["message"], Is.EqualTo(expectedErrorMessage));
        }

        [Test]
        public async Task OkResponseWithCustomSerializer_Returns200Status()
        {
            var response = await HttpClient.GetAsync($"{HttpApiUrlPrefix}/okresponsewithcustomserializerasync/John/Doe");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var person = JObject.Parse(content);
            Assert.That(person["FIRST_NAME"], Is.EqualTo("John"));
            Assert.That(person["LAST_NAME"], Is.EqualTo("Doe"));
        }
    }
}
