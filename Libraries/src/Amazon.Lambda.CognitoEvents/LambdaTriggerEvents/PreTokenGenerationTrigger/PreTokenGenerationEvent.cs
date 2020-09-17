using Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.Base;

namespace Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.PreTokenGenerationTrigger
{
    public class PreTokenGenerationEvent : TriggerEvent
    {
        new public PreTokenGenerationRequest Request { get; set; }
        new public PreTokenGenerationResponse Response { get; set; }
    }
}