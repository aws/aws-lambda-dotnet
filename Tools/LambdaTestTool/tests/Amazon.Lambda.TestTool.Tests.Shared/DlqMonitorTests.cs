using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.TestTool.Runtime;
using Xunit;

using Amazon.SQS;
using Amazon.SQS.Model;

namespace Amazon.Lambda.TestTool.Tests
{
    public class DlqMonitorTests
    {
        [Fact]
        public async Task DlqIntegTest()
        {
            if (!TestUtils.ProfileTestsEnabled)
                return;

            const int WAIT_TIME = 5000;
            var queueName = "local-dlq-list-queue-test-" + DateTime.Now.Ticks;
            using (var client = new AmazonSQSClient(TestUtils.GetAWSCredentials(), TestUtils.TestRegion))
            {
                var createResponse = await client.CreateQueueAsync(new CreateQueueRequest {QueueName = queueName});
                await TestUtils.WaitTillQueueIsCreatedAsync(client, createResponse.QueueUrl);
                try
                {
                    var configFile = TestUtils.GetLambdaFunctionSourceFile("ToUpperFunc", "aws-lambda-tools-defaults.json");
                    var buildPath = TestUtils.GetLambdaFunctionBuildPath("ToUpperFunc");

                    var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(configFile);
                    var runtime = LocalLambdaRuntime.Initialize(buildPath);
                    var function = runtime.LoadLambdaFunctions(configInfo.FunctionInfos)[0];

                    var monitor = new DlqMonitor(runtime, function, TestUtils.TestProfile, TestUtils.TestRegion.SystemName, createResponse.QueueUrl);
                    
                    monitor.Start();
                    await client.SendMessageAsync(new SendMessageRequest
                    {
                        QueueUrl = createResponse.QueueUrl,
                        MessageBody = "\"testing dlq\""
                    });
                    
                    Thread.Sleep(WAIT_TIME);
                    var logs = monitor.FetchNewLogs();
                    Assert.Single(logs);
                    
                    Assert.Contains("testing dlq", logs[0].Logs);
                    Assert.NotNull(logs[0].ReceiptHandle);
                    Assert.NotEqual(DateTime.MinValue, logs[0].ProcessTime);

                    logs = monitor.FetchNewLogs();
                    Assert.Equal(0, logs.Count);
                    
                    await client.SendMessageAsync(new SendMessageRequest
                    {
                        QueueUrl = createResponse.QueueUrl,
                        MessageBody = "\"testing dlq1\""
                    });
                    await client.SendMessageAsync(new SendMessageRequest
                    {
                        QueueUrl = createResponse.QueueUrl,
                        MessageBody = "\"testing dlq2\""
                    });
                    Thread.Sleep(WAIT_TIME);
                    
                    logs = monitor.FetchNewLogs();
                    Assert.Equal(2, logs.Count);
                    
                    monitor.Stop();
                    Thread.Sleep(WAIT_TIME);
                    await client.SendMessageAsync(new SendMessageRequest
                    {
                        QueueUrl = createResponse.QueueUrl,
                        MessageBody = "\"testing dlq3\""
                    });
                    Thread.Sleep(WAIT_TIME);
                    
                    logs = monitor.FetchNewLogs();
                    Assert.Equal(0, logs.Count);                    
                }
                finally 
                {
                    await client.DeleteQueueAsync(createResponse.QueueUrl);
                }                
            }
        }
    }
}