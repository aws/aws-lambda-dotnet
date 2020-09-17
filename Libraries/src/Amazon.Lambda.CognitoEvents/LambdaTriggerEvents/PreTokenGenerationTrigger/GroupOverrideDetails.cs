using System.Collections.Generic;

namespace Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.PreTokenGenerationTrigger
{
    public class GroupOverrideDetails
    {
        public IEnumerable<string> GroupsToOverride { get; set; }
        public IEnumerable<string> IamRolesToOverride { get; set; }
        public string PreferredRoe { get; set; }
    }
}