using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-pre-authentication.html
    /// </summary>
    [DataContract]
    public class CognitoPreAuthenticationRequest : CognitoTriggerRequest
    {
        /// <summary>
        /// One or more name-value pairs containing the validation data in the request to register a user. The validation data is set and then passed from the client in the request to register a user. You can pass this data to your Lambda function by using the ClientMetadata parameter in the InitiateAuth and AdminInitiateAuth API actions.
        /// </summary>
        [DataMember(Name = "validationData")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("validationData")]
# endif
        public Dictionary<string, string> ValidationData { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// This boolean is populated when PreventUserExistenceErrors is set to ENABLED for your User Pool client.
        /// </summary>
        [DataMember(Name = "userNotFound")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("userNotFound")]
#endif
        public bool UserNotFound { get; set; }
    }
}
