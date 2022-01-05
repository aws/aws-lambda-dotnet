using Xunit;

namespace TestServerlessApp.IntegrationTests
{
    [CollectionDefinition("Integration Tests")]
    public class IntegrationTestContextFixtureCollection : ICollectionFixture<IntegrationTestContextFixture>
    {
    }
}