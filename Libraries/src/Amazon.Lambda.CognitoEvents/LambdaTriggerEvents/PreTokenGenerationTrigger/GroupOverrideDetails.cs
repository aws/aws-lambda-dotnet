using System.Collections.Generic;

namespace Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.PreTokenGenerationTrigger
{
    /// <summary>
    /// The output object containing the current group configuration.
    /// It includes GroupsToOverride, IamRolesToOverride, and PreferredRole.
    /// </summary>
    public class GroupOverrideDetails
    {
        /// <summary>
        /// A list of the group names that are associated with the user that the identity token is issued for. 
        /// </summary>
        public IEnumerable<string> GroupsToOverride { get; set; }

        /// <summary>
        /// A list of the current IAM roles associated with these groups.
        /// </summary>
        public IEnumerable<string> IamRolesToOverride { get; set; }

        /// <summary>
        /// A string indicating the preferred IAM role.
        /// </summary>
        public string PreferredRole { get; set; }
    }
}