using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace TestServerlessApp.IntegrationTests
{
    [Collection("Integration Tests")]
    public class DynamoDBEventSourceMapping
    {
        private readonly IntegrationTestContextFixture _fixture;

        public DynamoDBEventSourceMapping(IntegrationTestContextFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task VerifyDynamoDBEventSourceMappingConfiguration()
        {
            var lambdaFunctionName = _fixture.LambdaFunctions.FirstOrDefault(x => string.Equals(x.LogicalId, "DynamoDBStreamHandler"))?.Name;
            Assert.NotNull(lambdaFunctionName);

            var testTableStreamArn = _fixture.TestTableStreamARN;
            Assert.False(string.IsNullOrEmpty(testTableStreamArn), "TestTable stream ARN should not be empty");

            var listEventSourceMappingResponse = await _fixture.LambdaHelper.ListEventSourceMappingsAsync(lambdaFunctionName, testTableStreamArn);
            var eventSourceMappings = listEventSourceMappingResponse.EventSourceMappings;

            Assert.Single(eventSourceMappings);

            var dynamoDbEventSourceMapping = eventSourceMappings.First();

            Assert.Equal(testTableStreamArn, dynamoDbEventSourceMapping.EventSourceArn);
            Assert.Equal(100, dynamoDbEventSourceMapping.BatchSize);
            Assert.Equal("TRIM_HORIZON", dynamoDbEventSourceMapping.StartingPosition);
        }
    }
}
