using Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.Base;

namespace Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.PreTokenGenerationTrigger
{
    /// <summary>
    /// The request from the Amazon Cognito service.
    /// </summary>
    public class PreTokenGenerationRequest : TriggerRequest
    {
        /// <summary>
        /// The input object containing the current group configuration.
        /// It includes GroupsToOverride, IamRolesToOverride, and PreferredRole.
        /// </summary>
        public GroupConfiguration GroupConfiguration { get; set; }
    }
}