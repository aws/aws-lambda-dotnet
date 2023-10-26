using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-custom-sms-sender.html
    /// </summary>
    public class CognitoCustomSmsSenderRequest : CognitoTriggerRequest
    {
        /// <summary>
        /// The type of sender request.
        /// </summary>
        [DataMember(Name = "type")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("type")]
#endif
        public string Type { get; set; }

        /// <summary>
        /// The encrypted temporary authorization code.
        /// </summary>
        [DataMember(Name = "code")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("code")]
#endif
        public string Code { get; set; }
    }
}
