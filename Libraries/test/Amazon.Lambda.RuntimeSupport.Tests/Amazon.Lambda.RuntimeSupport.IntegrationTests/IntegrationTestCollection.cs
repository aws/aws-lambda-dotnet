using Xunit;

namespace Amazon.Lambda.RuntimeSupport.IntegrationTests;

[CollectionDefinition("Integration Tests", DisableParallelization = true)]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>, ICollectionFixture<ResponseStreamingTestsFixture>
{
    
}
