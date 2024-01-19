using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-pre-token-generation.html
    /// </summary>
    [DataContract]
    public class ClaimsAndScopeOverrideDetails
    {
        /// <summary>
        /// The claims that you want to override, add, or suppress in your user’s ID token.
        /// </summary>
        [DataMember(Name = "idTokenGeneration")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("idTokenGeneration")]
# endif
        public IdTokenGeneration IdTokenGeneration { get; set; } = new IdTokenGeneration();

        /// <summary>
        /// The claims and scopes that you want to override, add, or suppress in your user’s access token.
        /// </summary>
        [DataMember(Name = "accessTokenGeneration")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("accessTokenGeneration")]
# endif
        public AccessTokenGeneration AccessTokenGeneration { get; set; } = new AccessTokenGeneration();
        
        /// <summary>
        /// The output object containing the current group configuration. It includes groupsToOverride, iamRolesToOverride, and preferredRole.
        /// </summary>
        [DataMember(Name = "groupOverrideDetails")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("groupOverrideDetails")]
# endif
        public GroupConfiguration GroupOverrideDetails { get; set; } = new GroupConfiguration();
    }
}
