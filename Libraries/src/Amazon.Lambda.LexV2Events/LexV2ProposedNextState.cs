namespace Amazon.Lambda.LexV2Events
{
    /// <summary>
    /// Contains information about the next action that the bot will take if the Lambda function sets the dialogAction to Delegate.
    /// https://docs.aws.amazon.com/lexv2/latest/dg/lambda.html#request-proposednextstate
    /// </summary>
    public class LexV2ProposedNextState
    {
        /// <summary>
        /// Dialog action that Amazon Lex V2 will perform next.
        /// </summary>
        public LexV2DialogAction DialogAction { get; set; }

        /// <summary>
        /// Intent that the bot has determined that the user is trying to fulfill.
        /// </summary>
        public LexV2Intent Intent { get; set; }
    }
}
