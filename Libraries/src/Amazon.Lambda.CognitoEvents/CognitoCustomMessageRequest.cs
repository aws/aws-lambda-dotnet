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
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("codeParameter")]
#endif
        public string CodeParameter { get; set; }

        /// <summary>
        /// The username parameter. It is a required request parameter for the admin create user flow.
        /// </summary>
        [DataMember(Name = "usernameParameter")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("usernameParameter")]
#endif
        public string UsernameParameter { get; set; }

        /// <summary>
        /// One or more key-value pairs that you can provide as custom input to the Lambda function that you specify for the pre sign-up trigger. You can pass this data to your Lambda function by using the ClientMetadata parameter in the following API actions: AdminCreateUser, AdminRespondToAuthChallenge, ForgotPassword, and SignUp.
        /// </summary>
        [DataMember(Name = "clientMetadata")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("clientMetadata")]
#endif
        public Dictionary<string, string> ClientMetadata { get; set; } = new Dictionary<string, string>();
    }
}
