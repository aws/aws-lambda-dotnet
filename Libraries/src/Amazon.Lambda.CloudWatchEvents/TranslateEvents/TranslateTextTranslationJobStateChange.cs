namespace Amazon.Lambda.CloudWatchEvents.TranslateEvents
{
    /// <summary>
    /// This class represents the details of a Translate Text Translation Job State Change sent via EventBridge.
    /// For more see - https://docs.aws.amazon.com/translate/latest/dg/monitoring-with-eventbridge.html
    /// </summary>
    public class TranslateTextTranslationJobStateChange
    {
        /// <summary>
        /// The translation job id.
        /// </summary>
        public string JobId { get; set; }

        /// <summary>
        /// The translation job status.
        /// </summary>
        public string JobStatus { get; set; }
    }
}
