using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class Messaging_MessageHandlerForNewFifoQueueUsingFnSubForQueueName_Generated
    {
        private readonly Messaging messaging;

        public Messaging_MessageHandlerForNewFifoQueueUsingFnSubForQueueName_Generated()
        {
            SetExecutionEnvironment();
            messaging = new Messaging();
        }

        public async System.Threading.Tasks.Task<int> MessageHandlerForNewFifoQueueUsingFnSubForQueueName(Amazon.Lambda.SQSEvents.SQSEvent.SQSMessage message, Amazon.Lambda.Core.ILambdaContext __context__)
        {
            return await messaging.MessageHandlerForNewFifoQueueUsingFnSubForQueueName(message, __context__);
        }

        private static void SetExecutionEnvironment()
        {
            const string envName = "AWS_EXECUTION_ENV";

            var envValue = new StringBuilder();

            // If there is an existing execution environment variable add the annotations package as a suffix.
            if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envName)))
            {
                envValue.Append($"{Environment.GetEnvironmentVariable(envName)}_");
            }

            envValue.Append("amazon-lambda-annotations_0.6.0.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}