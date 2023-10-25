using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-create-auth-challenge.html
    /// </summary>
    public class CognitoCreateAuthChallengeResponse : CognitoTriggerResponse
    {
        /// <summary>
        /// One or more key-value pairs for the client app to use in the challenge to be presented to the user.This parameter should contain all of the necessary information to accurately present the challenge to the user.
        /// </summary>
        [DataMember(Name = "publicChallengeParameters")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("publicChallengeParameters")]
#endif
        public Dictionary<string, string> PublicChallengeParameters { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// This parameter is only used by the Verify Auth Challenge Response Lambda trigger. This parameter should contain all of the information that is required to validate the user's response to the challenge. In other words, the publicChallengeParameters parameter contains the question that is presented to the user and privateChallengeParameters contains the valid answers for the question.
        /// </summary>
        [DataMember(Name = "privateChallengeParameters")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("privateChallengeParameters")]
#endif
        public Dictionary<string, string> PrivateChallengeParameters { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Your name for the custom challenge, if this is a custom challenge.
        /// </summary>
        [DataMember(Name = "challengeMetadata")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("challengeMetadata")]
#endif
        public string ChallengeMetadata { get; set; }
    }
}
