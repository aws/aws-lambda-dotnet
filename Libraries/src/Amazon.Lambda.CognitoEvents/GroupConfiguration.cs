using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-pre-token-generation.html
    /// </summary>
    [DataContract]
    public class GroupConfiguration
    {
        /// <summary>
        /// A list of the group names that are associated with the user that the identity token is issued for.
        /// </summary>
        [DataMember(Name = "groupsToOverride")]
        [System.Text.Json.Serialization.JsonPropertyName("groupsToOverride")]
        public List<string> GroupsToOverride { get; set; } = new List<string>();

        /// <summary>
        /// A list of the current IAM roles associated with these groups.
        /// </summary>
        [DataMember(Name = "iamRolesToOverride")]
        [System.Text.Json.Serialization.JsonPropertyName("iamRolesToOverride")]
        public List<string> IamRolesToOverride { get; set; } = new List<string>();

        /// <summary>
        /// A string indicating the preferred IAM role.
        /// </summary>
        [DataMember(Name = "preferredRole")]
        [System.Text.Json.Serialization.JsonPropertyName("preferredRole")]
        public string PreferredRole { get; set; }
    }
}
