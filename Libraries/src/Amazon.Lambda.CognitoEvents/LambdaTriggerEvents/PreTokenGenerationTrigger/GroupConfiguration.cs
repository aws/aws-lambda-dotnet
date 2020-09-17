using System.Collections.Generic;

namespace Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.PreTokenGenerationTrigger
{
    public class GroupConfiguration
    {
        public IEnumerable<string> GroupsToOverride { get; set; }
        public IEnumerable<string> IamRolesToOverride { get; set; }
        public string PreferredRole { get; set; }
        public IDictionary<string, string> ClientMetadata { get; set; }
    }
}