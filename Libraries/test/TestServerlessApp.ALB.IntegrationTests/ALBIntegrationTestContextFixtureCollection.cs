using Xunit;

namespace TestServerlessApp.ALB.IntegrationTests
{
    [CollectionDefinition("ALB Integration Tests")]
    public class ALBIntegrationTestContextFixtureCollection : ICollectionFixture<ALBIntegrationTestContextFixture>
    {
    }
}
