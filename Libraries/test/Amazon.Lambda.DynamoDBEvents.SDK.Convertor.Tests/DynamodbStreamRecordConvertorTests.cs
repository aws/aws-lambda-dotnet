namespace Amazon.Lambda.DynamoDBEvents.SDK.Convertor.Tests
{
    public class DynamodbStreamRecordConvertorTests
    {
        [Fact]
        public void ConvertToSdkStreamRecord_NullLambdaStreamRecord_ReturnsNull()
        {
            // Arrange
            DynamoDBEvent.StreamRecord lambdaStreamRecord = null;

            // Act
            var result = lambdaStreamRecord.ConvertToSdkStreamRecord();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToSdkStreamRecord_ValidLambdaStreamRecord_ReturnsSdkStreamRecord()
        {
            // Arrange
            var lambdaStreamRecord = new DynamoDBEvent.StreamRecord
            {
                ApproximateCreationDateTime = DateTime.UtcNow,
                Keys = new Dictionary<string, DynamoDBEvent.AttributeValue>
                {
                    { "Id", new DynamoDBEvent.AttributeValue { S = "123" } }
                },
                NewImage = new Dictionary<string, DynamoDBEvent.AttributeValue>
                {
                    { "Name", new DynamoDBEvent.AttributeValue { S = "Test" } }
                },
                OldImage = new Dictionary<string, DynamoDBEvent.AttributeValue>
                {
                    { "Name", new DynamoDBEvent.AttributeValue { S = "OldTest" } }
                },
                SequenceNumber = "1234567890",
                SizeBytes = 1024,
                StreamViewType = "NEW_AND_OLD_IMAGES"
            };

            // Act
            var result = lambdaStreamRecord.ConvertToSdkStreamRecord();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(lambdaStreamRecord.ApproximateCreationDateTime, result.ApproximateCreationDateTime);
            Assert.Equal(lambdaStreamRecord.SequenceNumber, result.SequenceNumber);
            Assert.Equal(lambdaStreamRecord.SizeBytes, result.SizeBytes);
            Assert.Equal(lambdaStreamRecord.StreamViewType, result.StreamViewType);

            Assert.NotNull(result.Keys);
            Assert.Single(result.Keys);
            Assert.Equal(lambdaStreamRecord.Keys["Id"].S, result.Keys["Id"].S);

            Assert.NotNull(result.NewImage);
            Assert.Single(result.NewImage);
            Assert.Equal(lambdaStreamRecord.NewImage["Name"].S, result.NewImage["Name"].S);

            Assert.NotNull(result.OldImage);
            Assert.Single(result.OldImage);
            Assert.Equal(lambdaStreamRecord.OldImage["Name"].S, result.OldImage["Name"].S);
        }
    }
}

