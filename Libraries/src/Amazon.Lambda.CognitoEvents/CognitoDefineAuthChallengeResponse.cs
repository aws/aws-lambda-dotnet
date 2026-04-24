using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-define-auth-challenge.html
    /// </summary>
    public class CognitoDefineAuthChallengeResponse : CognitoTriggerResponse
    {
        /// <summary>
        /// A string containing the name of the next challenge. If you want to present a new challenge to your user, specify the challenge name here.
        /// </summary>
        [DataMember(Name = "challengeName")]
        [System.Text.Json.Serialization.JsonPropertyName("challengeName")]
        public string ChallengeName { get; set; }

        /// <summary>
        /// Set to true if you determine that the user has been sufficiently authenticated by completing the challenges, or false otherwise.
        /// </summary>
        [DataMember(Name = "issueTokens")]
        [System.Text.Json.Serialization.JsonPropertyName("issueTokens")]
        public bool? IssueTokens { get; set; }

        /// <summary>
        /// Set to true if you want to terminate the current authentication process, or false otherwise.
        /// </summary>
        [DataMember(Name = "failAuthentication")]
        [System.Text.Json.Serialization.JsonPropertyName("failAuthentication")]
        public bool? FailAuthentication { get; set; }
    }
}
