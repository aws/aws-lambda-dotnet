using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using Amazon.Lambda.TestTool.Services;
using Xunit;

using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;

namespace Amazon.Lambda.TestTool.Tests
{
    public class AwsServiceTests
    {
        [Fact]
        public void ListProfiles()
        {
            if (!TestUtils.ProfileTestsEnabled)
                return;

            var aws = new AWSServiceImpl();

            var profiles = aws.ListProfiles();
            Assert.NotEmpty(profiles);

            foreach (var profile in profiles)
            {
                Assert.False(string.IsNullOrWhiteSpace(profile));
            }
            
            Assert.True(profiles.Contains(TestUtils.TestProfile));
        }

        [Fact]
        public async Task ListQueuesAsync()
        {
            if (!TestUtils.ProfileTestsEnabled)
                return;

            var queueName = "local-reader-list-queue-test-" + DateTime.Now.Ticks;
            using (var client = new AmazonSQSClient(TestUtils.GetAWSCredentials(), TestUtils.TestRegion))
            {
                var createResponse = await client.CreateQueueAsync(new CreateQueueRequest {QueueName = queueName});
                await TestUtils.WaitTillQueueIsCreatedAsync(client, createResponse.QueueUrl);
                try
                {
                    var aws = new AWSServiceImpl();
                    var queues = await aws.ListQueuesAsync(TestUtils.TestProfile, TestUtils.TestRegion.SystemName);
                    
                    Assert.True(queues.Contains(createResponse.QueueUrl));
                }
                finally
                {
                    await client.DeleteQueueAsync(createResponse.QueueUrl);
                }
            }
        }
        
        [Fact]
        public async Task ReadMessageAsync()
        {
            if (!TestUtils.ProfileTestsEnabled)
                return;

            var queueName = "local-reader-read-message-test-" + DateTime.Now.Ticks;
            using (var client = new AmazonSQSClient(TestUtils.GetAWSCredentials(), TestUtils.TestRegion))
            {
                var createResponse = await client.CreateQueueAsync(new CreateQueueRequest {QueueName = queueName});
                await TestUtils.WaitTillQueueIsCreatedAsync(client, createResponse.QueueUrl);
                try
                {
                    var aws = new AWSServiceImpl();
                    var message = await aws.ReadMessageAsync(TestUtils.TestProfile, TestUtils.TestRegion.SystemName, createResponse.QueueUrl);
                    
                    Assert.Null(message);

                    await client.SendMessageAsync(new SendMessageRequest
                    {
                        MessageBody = "data",
                        QueueUrl = createResponse.QueueUrl
                    });
                    
                    message = await aws.ReadMessageAsync(TestUtils.TestProfile, TestUtils.TestRegion.SystemName, createResponse.QueueUrl);
                    Assert.NotNull(message);
                    
                    await aws.DeleteMessageAsync(TestUtils.TestProfile, TestUtils.TestRegion.SystemName, createResponse.QueueUrl, message.ReceiptHandle);
                }
                finally
                {
                    await client.DeleteQueueAsync(createResponse.QueueUrl);
                }
            }
        }
    }
}