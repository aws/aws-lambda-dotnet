using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-verify-auth-challenge-response.html
    /// </summary>
    public class CognitoVerifyAuthChallengeRequest : CognitoTriggerRequest
    {
        /// <summary>
        /// This parameter comes from the Create Auth Challenge trigger, and is compared against a user’s challengeAnswer to determine whether the user passed the challenge.
        /// </summary>
        [DataMember(Name = "privateChallengeParameters")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("privateChallengeParameters")]
# endif
        public Dictionary<string, string> PrivateChallengeParameters { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// This parameter comes from the Create Auth Challenge trigger, and is compared against a user’s challengeAnswer to determine whether the user passed the challenge.
        /// </summary>
        [DataMember(Name = "challengeAnswer")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("challengeAnswer")]
# endif
        public string ChallengeAnswer { get; set; } = string.Empty;

        /// <summary>
        /// One or more key-value pairs that you can provide as custom input to the Lambda function that you specify for the pre sign-up trigger. You can pass this data to your Lambda function by using the ClientMetadata parameter in the following API actions: AdminVerifyUser, AdminRespondToAuthChallenge, ForgotPassword, and SignUp.
        /// </summary>
        [DataMember(Name = "clientMetadata")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("clientMetadata")]
# endif
        public Dictionary<string, string> ClientMetadata { get; set; }

        /// <summary>
        /// This boolean is populated when PreventUserExistenceErrors is set to ENABLED for your User Pool client.
        /// </summary>
        [DataMember(Name = "userNotFound")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("userNotFound")]
# endif
        public bool UserNotFound { get; set; }
    }
}
