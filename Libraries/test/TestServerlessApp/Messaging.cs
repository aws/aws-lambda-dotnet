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
        public Task MessageHandlerForPreExisingQueue(SQSEvent.SQSMessage message, ILambdaContext context)
        {
            return Task.CompletedTask;
        }
    }
}