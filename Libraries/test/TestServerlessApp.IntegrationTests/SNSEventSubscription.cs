using System.Linq;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Xunit;

namespace TestServerlessApp.IntegrationTests
{
    [Collection("Integration Tests")]
    public class SNSEventSubscription
    {
        private readonly IntegrationTestContextFixture _fixture;

        public SNSEventSubscription(IntegrationTestContextFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task VerifySNSSubscriptionConfiguration()
        {
            var lambdaFunctionName = _fixture.LambdaFunctions.FirstOrDefault(x => string.Equals(x.LogicalId, "SNSMessageHandler"))?.Name;
            Assert.NotNull(lambdaFunctionName);

            var testTopicArn = _fixture.TestTopicARN;
            Assert.False(string.IsNullOrEmpty(testTopicArn), "TestTopic ARN should not be empty");

            var snsClient = new AmazonSimpleNotificationServiceClient(Amazon.RegionEndpoint.USWest2);
            var subscriptions = await snsClient.ListSubscriptionsByTopicAsync(testTopicArn);

            // Find the Lambda subscription
            var lambdaSub = subscriptions.Subscriptions.FirstOrDefault(s =>
                s.Protocol == "lambda" && s.Endpoint.Contains(lambdaFunctionName));
            Assert.NotNull(lambdaSub);

            // Verify filter policy
            var attrs = await snsClient.GetSubscriptionAttributesAsync(lambdaSub.SubscriptionArn);
            Assert.True(attrs.Attributes.ContainsKey("FilterPolicy"));
            Assert.Contains("example_corp", attrs.Attributes["FilterPolicy"]);
        }
    }
}
