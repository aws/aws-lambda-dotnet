using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-pre-token-generation.html
    /// </summary>
    [DataContract]
    public class AccessTokenGeneration
    {
        /// <summary>
        /// A map of one or more key-value pairs of claims to add or override. For group related claims, use
        /// groupOverrideDetails instead.
        /// </summary>
        [DataMember(Name = "claimsToAddOrOverride")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("claimsToAddOrOverride")]
# endif
        public Dictionary<string, string> ClaimsToAddOrOverride { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// A list that contains claims to be suppressed from the identity token.
        /// </summary>
        [DataMember(Name = "claimsToSuppress")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("claimsToSuppress")]
# endif
        public List<string> ClaimsToSuppress { get; set; } = new List<string>();
        
        /// <summary>
        /// A list of OAuth 2.0 scopes that you want to add to the scope claim in your user's access token. You can't
        /// add scope values that contain one or more blank-space characters.
        /// </summary>
        [DataMember(Name = "scopesToAdd")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("scopesToAdd")]
# endif
        public List<string> ScopesToAdd { get; set; } = new List<string>();
        
        /// <summary>
        /// A list of OAuth 2.0 scopes that you want to remove from the scope claim in your user's access token.
        /// </summary>
        [DataMember(Name = "scopesToSuppress")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("scopesToSuppress")]
# endif
        public List<string> ScopesToSuppress { get; set; } = new List<string>();
    }
}
