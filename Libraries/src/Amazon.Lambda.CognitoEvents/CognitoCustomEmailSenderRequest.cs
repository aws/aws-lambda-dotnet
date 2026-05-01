using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-custom-email-sender.html
    /// </summary>
    public class CognitoCustomEmailSenderRequest : CognitoTriggerRequest
    {
        /// <summary>
        /// The type of sender request.
        /// </summary>
        [DataMember(Name = "type")]
        [System.Text.Json.Serialization.JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// The encrypted temporary authorization code.
        /// </summary>
        [DataMember(Name = "code")]
        [System.Text.Json.Serialization.JsonPropertyName("code")]
        public string Code { get; set; }
    }
}
