using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.AnnotationTests
{
    // I am putting this here because there is no Amazon.Lambda.Annotations.Tests project
    // I did not want to add a project in a pull request without asking first
    public class SqsMessageAttributeTest
    {
        [Fact]
        public void QueueNameQueueLogicalIdMutuallyExclusive()
        {
            var target = new SqsMessageAttribute
            {
                QueueLogicalId = "MyLogicalId"
            };
            Assert.Throws<InvalidOperationException>(() => target.EventQueueARN = "MyQueueName");

            // other way around
            target = new SqsMessageAttribute
            {
                EventQueueARN = "arn:aws:sqs:us-east-1:968993296699:app-deploy-blue-LAVETRYB3JKX-SomeQueueName"
            };
            Assert.Throws<InvalidOperationException>(() => target.QueueLogicalId = "MyLogicalId");

        }
    }
}
