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
        [SqsMessage(QueueName = "MyMessageQueue", BatchSize = 10)]
        public Task MessageHandlerForPreExistingQueue(SQSEvent.SQSMessage message, ILambdaContext context)
        {
            return Task.CompletedTask;
        }

        [LambdaFunction]
        [SqsMessage(BatchSize = 10, QueueLogicalId = "QueueForMessageHandlerForNewQueue")]
        public Task MessageHandlerForNewQueue(SQSEvent.SQSMessage message, ILambdaContext context)
        {
            return Task.CompletedTask;
        }
    }
}