using Xunit;

namespace Amazon.Lambda.RuntimeSupport.IntegrationTests;

[CollectionDefinition("Integration Tests")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
    
}