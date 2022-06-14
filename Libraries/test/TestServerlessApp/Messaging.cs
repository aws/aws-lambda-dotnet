using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;

namespace TestServerlessApp
{
    public class Messaging
    {
        [LambdaFunction]
        [SqsMessage(EventQueueARN = "arn:aws:sqs:us-east-1:968993296699:app-deploy-blue-LAVETRYB3JKX-SomeQueueName", EventBatchSize = 11)]
        public Task MessageHandlerForPreExistingQueue(SQSEvent.SQSMessage message, ILambdaContext context)
        {
            return Task.CompletedTask;
        }

        [LambdaFunction]
        [SqsMessage(
            EventBatchSize = 12, 
            QueueLogicalId = "QueueForMessageHandlerForNewQueue", 
            VisibilityTimeout = 100, 
            ContentBasedDeduplication = true, 
            DeduplicationScope = "queue", 
            DelaySeconds = 5, 
            FifoQueue = true,
            FifoThroughputLimit = "perQueue",
            KmsDataKeyReusePeriodSeconds = 299,
            KmsMasterKeyId = "alias/aws/sqs",
            MaximumMessageSize = 1024,
            MessageRetentionPeriod = 60,
            QueueName = "thisismyqueuename.fifo",
            ReceiveMessageWaitTimeSeconds =5,
            RedriveAllowPolicy = "{ 'redrivePermission' : 'denyAll' }",
            RedrivePolicy = "{ 'deadLetterTargetArn': 'arn:somewhere', 'maxReceiveCount': 5 }",
            Tags = new string[]{ "keyname1=value1", "keyname2=value2" },
            EventFilterCriteria = new string[] { "Filter1", "Filter2" },
            EventMaximumBatchingWindowInSeconds = 31)]
        public Task MessageHandlerForNewQueue(SQSEvent.SQSMessage message, ILambdaContext context)
        {
            return Task.CompletedTask;
        }
    }
}