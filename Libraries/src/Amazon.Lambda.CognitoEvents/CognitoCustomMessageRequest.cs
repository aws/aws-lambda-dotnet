using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-custom-message.html
    /// </summary>
    public class CognitoCustomMessageRequest : CognitoTriggerRequest
    {
        /// <summary>
        /// A string for you to use as the placeholder for the verification code in the custom message.
        /// </summary>
        [DataMember(Name = "codeParameter")]
        [System.Text.Json.Serialization.JsonPropertyName("codeParameter")]
        public string CodeParameter { get; set; }

        /// <summary>
        /// The username parameter. It is a required request parameter for the admin create user flow.
        /// </summary>
        [DataMember(Name = "usernameParameter")]
        [System.Text.Json.Serialization.JsonPropertyName("usernameParameter")]
        public string UsernameParameter { get; set; }

        /// <summary>
        /// One or more key-value pairs that you can provide as custom input to the Lambda function that you specify for the pre sign-up trigger. You can pass this data to your Lambda function by using the ClientMetadata parameter in the following API actions: AdminCreateUser, AdminRespondToAuthChallenge, ForgotPassword, and SignUp.
        /// </summary>
        [DataMember(Name = "clientMetadata")]
        [System.Text.Json.Serialization.JsonPropertyName("clientMetadata")]
        public Dictionary<string, string> ClientMetadata { get; set; } = new Dictionary<string, string>();
    }
}
