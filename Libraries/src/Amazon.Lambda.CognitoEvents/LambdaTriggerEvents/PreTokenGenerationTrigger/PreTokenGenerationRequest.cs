using Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.Base;

namespace Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.PreTokenGenerationTrigger
{
    public class PreTokenGenerationRequest : TriggerRequest
    {
        public GroupConfiguration GroupConfiguration { get; set; }
    }
}