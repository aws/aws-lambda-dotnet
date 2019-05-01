using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using Amazon.Lambda.TestTool.ExternalCommands;
using Xunit;

using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;

namespace Amazon.Lambda.TestTool.Tests
{
    public class ExternalCommandsTests
    {
        [Fact(Skip = "Integration test being disabled temporarily. Enable test once a container with a profile is created")]
        public void ListProfiles()
        {
            var manager = new ExternalCommandManager();

            var profiles = manager.ListProfiles();
            Assert.NotEmpty(profiles);

            foreach (var profile in profiles)
            {
                Assert.False(string.IsNullOrWhiteSpace(profile));
            }
            
            Assert.True(profiles.Contains(TestUtils.TestProfile));
        }

        [Fact(Skip = "Integration test being disabled temporarily. Enable test once a container with a profile is created")]
        public async Task ListQueuesAsync()
        {
            var queueName = "local-reader-list-queue-test-" + DateTime.Now.Ticks;
            using (var client = new AmazonSQSClient(TestUtils.GetAWSCredentials(), TestUtils.TestRegion))
            {
                var createResponse = await client.CreateQueueAsync(new CreateQueueRequest {QueueName = queueName});
                await TestUtils.WaitTillQueueIsCreatedAsync(client, createResponse.QueueUrl);
                try
                {
                    var manager = new ExternalCommandManager();
                    var queues = manager.ListQueues(TestUtils.TestProfile, TestUtils.TestRegion.SystemName);
                    
                    Assert.True(queues.Contains(createResponse.QueueUrl));
                }
                finally
                {
                    await client.DeleteQueueAsync(createResponse.QueueUrl);
                }
            }
        }
        
        [Fact(Skip = "Integration test being disabled temporarily. Enable test once a container with a profile is created")]
        public async Task ReadMessageAsync()
        {
            var queueName = "local-reader-read-message-test-" + DateTime.Now.Ticks;
            using (var client = new AmazonSQSClient(TestUtils.GetAWSCredentials(), TestUtils.TestRegion))
            {
                var createResponse = await client.CreateQueueAsync(new CreateQueueRequest {QueueName = queueName});
                await TestUtils.WaitTillQueueIsCreatedAsync(client, createResponse.QueueUrl);
                try
                {
                    var manager = new ExternalCommandManager();
                    var message = manager.ReadMessage(TestUtils.TestProfile, TestUtils.TestRegion.SystemName, createResponse.QueueUrl);
                    
                    Assert.Null(message);

                    await client.SendMessageAsync(new SendMessageRequest
                    {
                        MessageBody = "data",
                        QueueUrl = createResponse.QueueUrl
                    });
                    
                    message = manager.ReadMessage(TestUtils.TestProfile, TestUtils.TestRegion.SystemName, createResponse.QueueUrl);
                    Assert.NotNull(message);
                    
                    manager.DeleteMessage(TestUtils.TestProfile, TestUtils.TestRegion.SystemName, createResponse.QueueUrl, message.ReceiptHandle);
                }
                finally
                {
                    await client.DeleteQueueAsync(createResponse.QueueUrl);
                }
            }
        }
    }
}