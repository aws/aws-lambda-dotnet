using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-pre-token-generation.html
    /// </summary>
    [DataContract]
    public class ClaimOverrideDetails
    {
        /// <summary>
        /// A map of one or more key-value pairs of claims to add or override. For group related claims, use groupOverrideDetails instead.
        /// </summary>
        [DataMember(Name = "claimsToAddOrOverride")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("claimsToAddOrOverride")]
# endif
        public Dictionary<string, string> ClaimsToAddOrOverride { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// A list that contains claims to be suppressed from the identity token.
        /// </summary>
        [DataMember(Name = "claimsToSuppress")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("claimsToSuppress")]
# endif
        public List<string> ClaimsToSuppress { get; set; } = new List<string>();

        /// <summary>
        /// The output object containing the current group configuration. It includes groupsToOverride, iamRolesToOverride, and preferredRole.
        /// </summary>
        [DataMember(Name = "groupOverrideDetails")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("groupOverrideDetails")]
# endif
        public GroupConfiguration GroupOverrideDetails { get; set; } = new GroupConfiguration();
    }
}
