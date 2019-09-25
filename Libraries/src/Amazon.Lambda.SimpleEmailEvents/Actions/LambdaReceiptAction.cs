namespace Amazon.Lambda.SimpleEmailEvents.Actions
{
    /// <summary>
    /// The lambda receipt's action.
    /// </summary>
    public class LambdaReceiptAction : IReceiptAction
    {
        /// <summary>
        /// The type of the action, e.g. "Lambda", "S3"
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The type of invocation, e.g. "Event"
        /// </summary>
        public string InvocationType { get; set; }

        /// <summary>
        /// The ARN of this function.
        /// </summary>
        public string FunctionArn { get; set; }
    }
}
