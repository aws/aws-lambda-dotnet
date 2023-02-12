using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-verify-auth-challenge-response.html
    /// </summary>
    public class CognitoVerifyAuthChallengeResponse : CognitoTriggerResponse
    {
        /// <summary>
        /// Set to true if the user has successfully completed the challenge, or false otherwise.
        /// </summary>
        [DataMember(Name = "answerCorrect")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("answerCorrect")]
#endif
        public bool AnswerCorrect { get; set; }
    }
}
