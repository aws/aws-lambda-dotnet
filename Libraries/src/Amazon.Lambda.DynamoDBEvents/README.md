# Amazon.Lambda.DynamoDBEvents

This package contains classes that can be used as input types for Lambda functions that process Amazon DynamoDB events.

This package has a dependency on the [AWS SDK for .NET package DynamoDBv2](https://www.nuget.org/packages/AWSSDK.DynamoDBv2/) in order to use the `Amazon.DynamoDBv2.Model.Record` type. 

# Sample Function

The following is a sample class and Lambda function that receives Amazon DynamoDB event record data as an input and writes some of the incoming event data to CloudWatch Logs. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```
public class Function
{
    public void Handler(DynamoDBEvent ddbEvent)
    {
        foreach (var record in ddbEvent.Records)
        {
            var ddbRecord = record.Dynamodb;
            var keys = string.Join(", ", ddbRecord.Keys.Keys);
            Console.WriteLine($"{record.EventID} - Keys = [{keys}], Size = {ddbRecord.SizeBytes} bytes");
        }
        Console.WriteLine($"Successfully processed {ddbEvent.Records.Count} records.");
    }
}
```
