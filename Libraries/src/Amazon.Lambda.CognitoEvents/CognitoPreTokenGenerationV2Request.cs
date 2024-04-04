using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-pre-token-generation.html
    /// </summary>
    public class CognitoPreTokenGenerationV2Request : CognitoTriggerRequest
    {
        /// <summary>
        /// The input object containing the current group configuration. It includes groupsToOverride, iamRolesToOverride, and preferredRole.
        /// </summary>
        [DataMember(Name = "groupConfiguration")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("groupConfiguration")]
# endif
        public GroupConfiguration GroupConfiguration { get; set; } = new GroupConfiguration();

        /// <summary>
        /// One or more key-value pairs that you can provide as custom input to the Lambda function that you specify for the pre sign-up trigger. You can pass this data to your Lambda function by using the ClientMetadata parameter in the following API actions: AdminVerifyUser, AdminRespondToAuthChallenge, ForgotPassword, and SignUp.
        /// </summary>
        [DataMember(Name = "clientMetadata")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("clientMetadata")]
# endif
        public Dictionary<string, string> ClientMetadata { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// A list that contains the OAuth 2.0 user scopes.
        /// </summary>
        [DataMember(Name = "scopes")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("scopes")]
# endif
        public List<string> Scopes { get; set; } = new List<string>();
    }
}
