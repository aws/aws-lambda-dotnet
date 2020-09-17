using Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.Base;

namespace Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.PreTokenGenerationTrigger
{
    /// <summary>
    /// Amazon Cognito invokes this trigger before token generation allowing you to customize identity token claims.
    /// This Lambda trigger allows you to customize an identity token before it is generated.
    /// You can use this trigger to add new claims, update claims, or suppress claims in the identity token.
    /// </summary>
    public class PreTokenGenerationEvent : TriggerEvent
    {
        /// <summary>
        /// The request from the Amazon Cognito service.
        /// </summary>
        public PreTokenGenerationRequest Request { get; set; }

        /// <summary>
        /// The response from your Lambda trigger.
        /// </summary>
        public PreTokenGenerationResponse Response { get; set; }
    }
}