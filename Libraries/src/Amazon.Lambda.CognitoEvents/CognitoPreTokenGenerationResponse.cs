using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-pre-token-generation.html
    /// </summary>
    public class CognitoPreTokenGenerationResponse : CognitoTriggerResponse
    {
        /// <summary>
        /// Pre token generation response parameters
        /// </summary>
        [DataMember(Name = "claimsOverrideDetails")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("claimsOverrideDetails")]
# endif
        public ClaimOverrideDetails ClaimsOverrideDetails { get; set; } = new ClaimOverrideDetails();
    }
}
