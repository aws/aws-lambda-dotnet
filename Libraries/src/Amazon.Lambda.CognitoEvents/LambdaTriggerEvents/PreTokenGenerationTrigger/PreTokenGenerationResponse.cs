namespace Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.PreTokenGenerationTrigger
{
    /// <summary>
    /// The response from your Lambda trigger.
    /// </summary>
    public class PreTokenGenerationResponse
    {
        /// <summary>
        /// The output object containing the current claims configuration
        /// </summary>
        public ClaimsOverrideDetails ClaimsOverrideDetails { get; set; }
    }
}