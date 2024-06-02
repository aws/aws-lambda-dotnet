using Amazon.Lambda.Annotations.SQS;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    public class SQSEventAttributeTests
    {
        [Fact]
        public void VerifyPropertyIsSet()
        {
            const string queueArn = "arn:aws:sqs:us-east-2:444455556666:queue1";

            var att = new SQSEventAttribute(queueArn);
            Assert.False(att.IsEnabledSet);
            Assert.False(att.IsBatchSizeSet);
            Assert.False(att.IsMaximumBatchingWindowInSecondsSet);
            Assert.False(att.IsFiltersSet);
            Assert.False(att.IsMaximumConcurrencySet);

            att = new SQSEventAttribute(queueArn)
            {
                BatchSize = 10,
                MaximumBatchingWindowInSeconds = 10,
                Filters = "Filter1;Filter2",
                MaximumConcurrency = 10,
                Enabled = true
            };

            Assert.True(att.IsEnabledSet);
            Assert.True(att.IsBatchSizeSet);
            Assert.True(att.IsMaximumBatchingWindowInSecondsSet);
            Assert.True(att.IsFiltersSet);
            Assert.True(att.IsMaximumConcurrencySet);

            att = new SQSEventAttribute(queueArn) { Filters = "" };
            Assert.True(att.IsFiltersSet);
        }
    }
}
