namespace Amazon.Lambda.CloudWatchEvents.TranscribeEvents
{
    /// <summary>
    /// This class represents the details of a Transcribe Job State Change sent via EventBridge.
    /// For more see - https://docs.aws.amazon.com/transcribe/latest/dg/monitoring-events.html
    /// </summary>
    public class TranscribeJobStateChange
    {
        /// <summary>
        /// The transcription job name.
        /// </summary>
        public string TranscriptionJobName { get; set; }

        /// <summary>
        /// The transcription job status.
        /// </summary>
        public string TranscriptionJobStatus { get; set; }

        /// <summary>
        /// If the TranscriptionJobStatus is FAILED, this field contains information about the failure.
        /// </summary>
        public string FailureReason { get; set; }
    }
}
