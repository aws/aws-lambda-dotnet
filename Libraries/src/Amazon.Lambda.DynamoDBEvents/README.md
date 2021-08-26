# Amazon.Lambda.DynamoDBEvents

This package contains classes that can be used as input types for Lambda functions that process Amazon DynamoDB events.

This package has a dependency on the [AWS SDK for .NET package DynamoDBv2](https://www.nuget.org/packages/AWSSDK.DynamoDBv2/) in order to use the `Amazon.DynamoDBv2.Model.Record` type. 

# Sample Function

The following is a sample class and Lambda function that receives Amazon DynamoDB event record data as an input and writes some of the incoming event data to CloudWatch Logs. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
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

The following is a sample class and Lambda function that receives Amazon DynamoDB event record data as an input and uses `StreamsEventResponse` object to return batch item failures, if any. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
public class Function
{
    public StreamsEventResponse Handler(DynamoDBEvent ddbEvent)
    {
        var batchItemFailures = new List<StreamsEventResponse.BatchItemFailure>();
        string curRecordSequenceNumber = string.Empty;

        foreach (var record in ddbEvent.Records)
        {
            try
            {
                //Process your record
                var ddbRecord = record.Dynamodb;
                var keys = string.Join(", ", ddbRecord.Keys.Keys);
                curRecordSequenceNumber = ddbRecord.SequenceNumber;
                Console.WriteLine($"{record.EventID} - Keys = [{keys}], Size = {ddbRecord.SizeBytes} bytes");
            }
            catch (Exception e)
            {
                //Return failed record's sequence number
                batchItemFailures.Add(new StreamsEventResponse.BatchItemFailure() { ItemIdentifier = curRecordSequenceNumber });
                return new StreamsEventResponse() { BatchItemFailures = batchItemFailures };
            }
        }
        Console.WriteLine($"Successfully processed {ddbEvent.Records.Count} records.");
        return new StreamsEventResponse() { BatchItemFailures = batchItemFailures };
    }
}
```
