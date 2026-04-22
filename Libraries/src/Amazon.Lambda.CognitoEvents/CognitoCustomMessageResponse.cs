using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-custom-message.html
    /// </summary>
    public class CognitoCustomMessageResponse : CognitoTriggerResponse
    {
        /// <summary>
        /// The custom SMS message to be sent to your users. Must include the codeParameter value received in the request.
        /// </summary>
        [DataMember(Name = "smsMessage")]
        [System.Text.Json.Serialization.JsonPropertyName("smsMessage")]
        public string SmsMessage { get; set; }

        /// <summary>
        /// The custom email message to be sent to your users. Must include the codeParameter value received in the request.
        /// </summary>
        [DataMember(Name = "emailMessage")]
        [System.Text.Json.Serialization.JsonPropertyName("emailMessage")]
        public string EmailMessage { get; set; }

        /// <summary>
        /// The subject line for the custom message.
        /// </summary>
        [DataMember(Name = "emailSubject")]
        [System.Text.Json.Serialization.JsonPropertyName("emailSubject")]
        public string EmailSubject { get; set; }
    }
}
