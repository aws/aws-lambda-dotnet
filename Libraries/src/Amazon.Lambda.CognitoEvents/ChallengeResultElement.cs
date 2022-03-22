using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-define-auth-challenge.html
    /// </summary>
    [DataContract]
    public class ChallengeResultElement
    {
        /// <summary>
        /// The challenge type.One of: CUSTOM_CHALLENGE, SRP_A, PASSWORD_VERIFIER, SMS_MFA, DEVICE_SRP_AUTH, DEVICE_PASSWORD_VERIFIER, or ADMIN_NO_SRP_AUTH.
        /// </summary>
        [DataMember(Name = "challengeName")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("challengeName")]
# endif
        public string ChallengeName { get; set; }

        /// <summary>
        /// Set to true if the user successfully completed the challenge, or false otherwise.
        /// </summary>
        [DataMember(Name = "challengeResult")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("challengeResult")]
# endif
        public bool ChallengeResult { get; set; }

        /// <summary>
        /// Your name for the custom challenge.Used only if challengeName is CUSTOM_CHALLENGE.
        /// </summary>
        [DataMember(Name = "challengeMetadata")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("challengeMetadata")]
# endif
        public string ChallengeMetadata { get; set; }
    }
}
