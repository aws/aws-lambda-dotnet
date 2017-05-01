# Amazon.Lambda.KinesisFirehoseEvents

This package contains classes that can be used for AWS Lambda functions that perform data transformations on records written into an Amazon Kinesis Firehose delivery stream.

# Sample Function

The following is a sample class and Lambda function that transforms Kinesis Firehose records by doing a ToUpper on the data.

```csharp
public class Function
{

    public KinesisFirehoseResponse FunctionHandler(KinesisFirehoseEvent evnt, ILambdaContext context)
    {
        context.Logger.LogLine($"InvocationId: {evnt.InvocationId}");
        context.Logger.LogLine($"DeliveryStreamArn: {evnt.DeliveryStreamArn}");
        context.Logger.LogLine($"Region: {evnt.Region}");

        var response = new KinesisFirehoseResponse
        {
            Records = new List<KinesisFirehoseResponse.FirehoseRecord>()
        };

        foreach (var record in evnt.Records)
        {
            context.Logger.LogLine($"\tRecordId: {record.RecordId}");
            context.Logger.LogLine($"\t\tApproximateArrivalEpoch: {record.ApproximateArrivalEpoch}");
            context.Logger.LogLine($"\t\tApproximateArrivalTimestamp: {record.ApproximateArrivalTimestamp}");
            context.Logger.LogLine($"\t\tData: {record.DecodeData()}");

            // Transform data: For example ToUpper the data
            var transformedRecord = new KinesisFirehoseResponse.FirehoseRecord
            {
                RecordId = record.RecordId,
                Result = KinesisFirehoseResponse.TRANSFORMED_STATE_OK                    
            };
            transformedRecord.EncodeData(record.DecodeData().ToUpperInvariant());

            response.Records.Add(transformedRecord);
        }

        return response;
    }
}
```
