using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-pre-token-generation.html
    /// </summary>
    [DataContract]
    public class IdTokenGeneration
    {
        /// <summary>
        /// A map of one or more key-value pairs of claims to add or override. For group related claims, use groupOverrideDetails instead.
        /// </summary>
        [DataMember(Name = "claimsToAddOrOverride")]
        [System.Text.Json.Serialization.JsonPropertyName("claimsToAddOrOverride")]
        public Dictionary<string, object> ClaimsToAddOrOverride { get; set; } = new Dictionary<string, object>();
        
        /// <summary>
        /// A list that contains claims to be suppressed from the identity token.
        /// </summary>
        [DataMember(Name = "claimsToSuppress")]
        [System.Text.Json.Serialization.JsonPropertyName("claimsToSuppress")]
        public List<string> ClaimsToSuppress { get; set; } = new List<string>();
    }
}
