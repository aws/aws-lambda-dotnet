using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-define-auth-challenge.html
    /// </summary>
    public class CognitoDefineAuthChallengeRequest : CognitoTriggerRequest
    {
        /// <summary>
        /// One or more key-value pairs that you can provide as custom input to the Lambda function that you specify for the pre sign-up trigger. You can pass this data to your Lambda function by using the ClientMetadata parameter in the following API actions: AdminCreateUser, AdminRespondToAuthChallenge, ForgotPassword, and SignUp.
        /// </summary>
        [DataMember(Name = "clientMetadata")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("clientMetadata")]
# endif
        public Dictionary<string, string> ClientMetadata { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// an array of ChallengeResult elements
        /// </summary>
        [DataMember(Name = "session")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("session")]
# endif
        public List<ChallengeResultElement> Session { get; set; } = new List<ChallengeResultElement>();

        /// <summary>
        /// A Boolean that is populated when PreventUserExistenceErrors is set to ENABLED for your user pool client. A value of true means that the user id (user name, email address, etc.) did not match any existing users. When PreventUserExistenceErrors is set to ENABLED, the service will not report back to the app that the user does not exist. The recommended best practice is for your Lambda functions to maintain the same user experience including latency so the caller cannot detect different behavior when the user exists or doesn’t exist.
        /// </summary>
        [DataMember(Name = "userNotFound")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("userNotFound")]
#endif
        public bool UserNotFound { get; set; }
    }
}
