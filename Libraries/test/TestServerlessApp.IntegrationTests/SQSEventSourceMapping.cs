using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace TestServerlessApp.IntegrationTests
{
    [Collection("Integration Tests")]
    public class SQSEventSourceMapping
    {
        private readonly IntegrationTestContextFixture _fixture;

        public SQSEventSourceMapping(IntegrationTestContextFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task VerifySQSEventSourceMappingConfiguration()
        {
            var lambdaFunctionName = _fixture.LambdaFunctions.FirstOrDefault(x => string.Equals(x.LogicalId, "SQSMessageHandler"))?.Name;
            var sqsQueueArn = _fixture.TestQueueARN;

            var listEventSourceMappingResponse = await _fixture.LambdaHelper.ListEventSourceMappingsAsync(lambdaFunctionName, sqsQueueArn);
            var eventSourceMappings = listEventSourceMappingResponse.EventSourceMappings;

            Assert.Single(eventSourceMappings);

            var sqsEventSourceMapping = eventSourceMappings.First();

            Assert.Equal(sqsQueueArn, sqsEventSourceMapping.EventSourceArn);
            Assert.Equal(50, sqsEventSourceMapping.BatchSize);
            Assert.Equal(5, sqsEventSourceMapping.MaximumBatchingWindowInSeconds);
            Assert.Equal(5, sqsEventSourceMapping.ScalingConfig.MaximumConcurrency);

            Assert.Single(sqsEventSourceMapping.FunctionResponseTypes);
            Assert.Equal("ReportBatchItemFailures", sqsEventSourceMapping.FunctionResponseTypes.First());

            var filters = sqsEventSourceMapping.FilterCriteria.Filters;
            Assert.Single(filters);
            Assert.Equal("{ \"body\" : { \"RequestCode\" : [ \"BBBB\" ] } }", filters.First().Pattern);
        }
    }
}
