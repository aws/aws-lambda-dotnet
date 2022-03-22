using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/cognito-user-identity-pools-working-with-aws-lambda-triggers.html#cognito-user-pools-lambda-trigger-syntax-shared
    /// </summary>
    [DataContract]
    public class CognitoTriggerCallerContext
    {
        /// <summary>
        /// The AWS SDK version number.
        /// </summary>
        [DataMember(Name = "awsSdkVersion")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("awsSdkVersion")]
#endif
        public string AwsSdkVersion { get; set; }

        /// <summary>
        /// The ID of the client associated with the user pool.
        /// </summary>
        [DataMember(Name = "clientId")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("clientId")]
#endif
        public string ClientId { get; set; }

    }
}
