using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.DynamoDB;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;

namespace TestServerlessApp
{
    public class DynamoDbStreamProcessing
    {
        [LambdaFunction(ResourceName = "DynamoDBStreamHandler", Policies = "AWSLambdaDynamoDBExecutionRole", PackageType = LambdaPackageType.Image)]
        [DynamoDBEvent("@TestTable", ResourceName = "TestTableStream", BatchSize = 100, StartingPosition = "TRIM_HORIZON")]
        public void HandleStream(DynamoDBEvent evnt, ILambdaContext lambdaContext)
        {
            lambdaContext.Logger.Log($"Received {evnt.Records.Count} records");
        }
    }
}
