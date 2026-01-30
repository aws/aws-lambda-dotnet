using Xunit;

namespace TestCustomAuthorizerApp.IntegrationTests;

[CollectionDefinition("Integration Tests")]
public class IntegrationTestContextFixtureCollection : ICollectionFixture<IntegrationTestContextFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
