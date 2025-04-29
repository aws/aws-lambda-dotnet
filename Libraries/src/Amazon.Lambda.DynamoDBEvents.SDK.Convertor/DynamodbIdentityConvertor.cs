namespace Amazon.Lambda.DynamoDBEvents.SDK.Convertor
{

    /// <summary>
    /// Convert DynamoDB Event Identity to SDK Identity
    /// </summary>
    public static class DynamodbIdentityConvertor
    {
        /// <summary>
        /// Convert Lambda Identity to SDK Identity
        /// </summary>
        /// <param name="lambdaIdentity">The Lambda Identity to convert.</param>
        /// <returns>The converted SDK Identity.</returns>
        public static Amazon.DynamoDBStreams.Model.Identity ConvertToSdkIdentity(this DynamoDBEvent.Identity lambdaIdentity)
        {
            if (lambdaIdentity == null)
                return null;

            var sdkIdentity = new Amazon.DynamoDBStreams.Model.Identity
            {
                PrincipalId = lambdaIdentity.PrincipalId,
                Type = lambdaIdentity.Type
            };

            return sdkIdentity;
        }
    }
}
