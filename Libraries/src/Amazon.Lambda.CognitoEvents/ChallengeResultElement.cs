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
        [System.Text.Json.Serialization.JsonPropertyName("challengeName")]
        public string ChallengeName { get; set; }

        /// <summary>
        /// Set to true if the user successfully completed the challenge, or false otherwise.
        /// </summary>
        [DataMember(Name = "challengeResult")]
        [System.Text.Json.Serialization.JsonPropertyName("challengeResult")]
        public bool ChallengeResult { get; set; }

        /// <summary>
        /// Your name for the custom challenge.Used only if challengeName is CUSTOM_CHALLENGE.
        /// </summary>
        [DataMember(Name = "challengeMetadata")]
        [System.Text.Json.Serialization.JsonPropertyName("challengeMetadata")]
        public string ChallengeMetadata { get; set; }
    }
}
