using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Amazon.Lambda.Core;
using Amazon.Lambda.KinesisFirehoseEvents;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace BlueprintBaseName._1
{
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
}