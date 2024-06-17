using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.SQS;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;

namespace TestServerlessApp
{
    public class SqsMessageProcessing
    {
        [LambdaFunction(ResourceName = "SQSMessageHandler", Policies = "AWSLambdaSQSQueueExecutionRole", PackageType = LambdaPackageType.Image)]
        [SQSEvent("@TestQueue", ResourceName = "TestQueueEvent", BatchSize = 50, MaximumConcurrency = 5, MaximumBatchingWindowInSeconds = 5, Filters = "{ \"body\" : { \"RequestCode\" : [ \"BBBB\" ] } }")]
        public SQSBatchResponse HandleMessage(SQSEvent evnt, ILambdaContext lambdaContext)
        {
            lambdaContext.Logger.Log($"Received {evnt.Records.Count} messages");
            return new SQSBatchResponse();
        }
    }
}
