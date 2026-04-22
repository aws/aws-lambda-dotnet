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
        [System.Text.Json.Serialization.JsonPropertyName("idTokenGeneration")]
        public IdTokenGeneration IdTokenGeneration { get; set; } = new IdTokenGeneration();

        /// <summary>
        /// The claims and scopes that you want to override, add, or suppress in your user’s access token.
        /// </summary>
        [DataMember(Name = "accessTokenGeneration")]
        [System.Text.Json.Serialization.JsonPropertyName("accessTokenGeneration")]
        public AccessTokenGeneration AccessTokenGeneration { get; set; } = new AccessTokenGeneration();
        
        /// <summary>
        /// The output object containing the current group configuration. It includes groupsToOverride, iamRolesToOverride, and preferredRole.
        /// </summary>
        [DataMember(Name = "groupOverrideDetails")]
        [System.Text.Json.Serialization.JsonPropertyName("groupOverrideDetails")]
        public GroupConfiguration GroupOverrideDetails { get; set; } = new GroupConfiguration();
    }
}
