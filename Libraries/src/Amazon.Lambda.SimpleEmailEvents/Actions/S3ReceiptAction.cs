namespace Amazon.Lambda.SimpleEmailEvents.Actions
{
    /// <summary>
    /// The S3 action's receipt
    /// </summary>
    public class S3ReceiptAction : IReceiptAction
    {
        /// <summary>
        /// The type of the action, e.g. "Lambda", "S3"
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The SNS topic to be posted to after the S3 action
        /// </summary>
        public string TopicArn { get; set; }

        /// <summary>
        /// The S3 bucket name where the email has been stored
        /// </summary>
        public string BucketName { get; set; }

        /// <summary>
        /// The prefix to the object's full key key (i.e folders)
        /// </summary>
        public string ObjectKeyPrefix { get; set; }

        /// <summary>
        /// The full object key
        /// </summary>
        public string ObjectKey { get; set; }
    }
}
