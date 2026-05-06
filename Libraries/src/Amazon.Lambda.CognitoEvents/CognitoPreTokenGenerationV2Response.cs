using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-pre-token-generation.html
    /// </summary>
    public class CognitoPreTokenGenerationV2Response : CognitoTriggerResponse
    {
        /// <summary>
        /// A container for all elements in a V2_0 trigger event.
        /// </summary>
        [DataMember(Name = "claimsAndScopeOverrideDetails")]
        [System.Text.Json.Serialization.JsonPropertyName("claimsAndScopeOverrideDetails")]
        public ClaimsAndScopeOverrideDetails ClaimsAndScopeOverrideDetails { get; set; } = new ClaimsAndScopeOverrideDetails();
    }
}
