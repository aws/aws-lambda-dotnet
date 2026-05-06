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
        [System.Text.Json.Serialization.JsonPropertyName("claimsOverrideDetails")]
        public ClaimOverrideDetails ClaimsOverrideDetails { get; set; } = new ClaimOverrideDetails();
    }
}
