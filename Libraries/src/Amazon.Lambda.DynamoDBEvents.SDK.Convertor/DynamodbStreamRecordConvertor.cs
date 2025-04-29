namespace Amazon.Lambda.DynamoDBEvents.SDK.Convertor
{
    /// <summary>
    /// Convert DynamoDB Event StreamRecord to SDK StreamRecord
    /// </summary>
    public static class DynamodbStreamRecordConvertor
    {

        /// <summary>
        /// Convert Lambda StreamRecord to SDK StreamRecord
        /// </summary>
        /// <param name="lambdaStreamRecord">The Lambda StreamRecord to convert.</param>
        /// <returns>The converted SDK StreamRecord.</returns>
        public static Amazon.DynamoDBStreams.Model.StreamRecord ConvertToSdkStreamRecord(this DynamoDBEvent.StreamRecord lambdaStreamRecord)
        {
            if (lambdaStreamRecord == null)
                return null;

            var sdkStreamRecord = new Amazon.DynamoDBStreams.Model.StreamRecord
            {
                ApproximateCreationDateTime = lambdaStreamRecord.ApproximateCreationDateTime,
                Keys = lambdaStreamRecord.Keys?.ConvertToSdkStreamAttributeValueDictionary(),
                NewImage = lambdaStreamRecord.NewImage?.ConvertToSdkStreamAttributeValueDictionary(),
                OldImage = lambdaStreamRecord.OldImage?.ConvertToSdkStreamAttributeValueDictionary(),
                SequenceNumber = lambdaStreamRecord.SequenceNumber,
                SizeBytes = lambdaStreamRecord.SizeBytes,
                StreamViewType = lambdaStreamRecord.StreamViewType
            };

            return sdkStreamRecord;
        }

    }
}
