using Amazon.Lambda.Core;
using Amazon.Lambda.KinesisFirehoseEvents;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace BlueprintBaseName._1;

public class Function
{

    public KinesisFirehoseResponse FunctionHandler(KinesisFirehoseEvent evnt, ILambdaContext context)
    {
        context.Logger.LogInformation($"InvocationId: {evnt.InvocationId}");
        context.Logger.LogInformation($"DeliveryStreamArn: {evnt.DeliveryStreamArn}");
        context.Logger.LogInformation($"Region: {evnt.Region}");

        var response = new KinesisFirehoseResponse
        {
            Records = new List<KinesisFirehoseResponse.FirehoseRecord>()
        };

        foreach (var record in evnt.Records)
        {
            context.Logger.LogInformation($"\tRecordId: {record.RecordId}");
            context.Logger.LogInformation($"\t\tApproximateArrivalEpoch: {record.ApproximateArrivalEpoch}");
            context.Logger.LogInformation($"\t\tApproximateArrivalTimestamp: {record.ApproximateArrivalTimestamp}");
            context.Logger.LogInformation($"\t\tData: {record.DecodeData()}");

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