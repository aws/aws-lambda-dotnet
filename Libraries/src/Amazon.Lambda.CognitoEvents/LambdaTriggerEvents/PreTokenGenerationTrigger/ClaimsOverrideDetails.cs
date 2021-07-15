using System.Collections.Generic;

namespace Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.PreTokenGenerationTrigger
{
    /// <summary>
    /// The output object containing the current claims configuration
    /// </summary>
    public class ClaimsOverrideDetails
    {
        /// <summary>
        /// A map of one or more key-value pairs of claims to add or override.
        /// For group related claims, use groupOverrideDetails instead.
        /// </summary>
        public IDictionary<string, string> ClaimsToAddOrOverride { get; set; }

        /// <summary>
        /// A list that contains claims to be suppressed from the identity token.
        /// </summary>
        public IEnumerable<string> ClaimsToSuppress { get; set; }

        /// <summary>
        /// The output object containing the current group configuration.
        /// It includes GroupsToOverride, IamRolesToOverride, and PreferredRole.
        /// </summary>
        public GroupOverrideDetails GroupOverrideDetails { get; set; }
    }
}