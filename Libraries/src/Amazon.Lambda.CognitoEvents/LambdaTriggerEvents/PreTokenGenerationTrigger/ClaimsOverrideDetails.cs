using System.Collections.Generic;

namespace Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.PreTokenGenerationTrigger
{
    public class ClaimsOverrideDetails
    {
        public IDictionary<string, string> ClaimsToAddOrOverride { get; set; }
        public IEnumerable<string> ClaimsToSuppress { get; set; }
        public GroupOverrideDetails GroupOverrideDetails { get; set; }
    }
}