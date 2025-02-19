namespace Amazon.Lambda.DynamoDBEvents.SDK.Convertor.Tests
{
    public class DynamodbIdentityConvertorTests
    {
        [Fact]
        public void ConvertToSdkIdentity_NullLambdaIdentity_ReturnsNull()
        {
            // Arrange
            DynamoDBEvent.Identity lambdaIdentity = null;

            // Act
            var result = lambdaIdentity.ConvertToSdkIdentity();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToSdkIdentity_ValidLambdaIdentity_ReturnsSdkIdentity()
        {
            // Arrange
            var lambdaIdentity = new DynamoDBEvent.Identity
            {
                PrincipalId = "dynamodb.amazonaws.com",
                Type = "Service"
            };

            // Act
            var result = lambdaIdentity.ConvertToSdkIdentity();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(lambdaIdentity.PrincipalId, result.PrincipalId);
            Assert.Equal(lambdaIdentity.Type, result.Type);
        }
    }
}
