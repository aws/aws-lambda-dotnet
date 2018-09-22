namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// An object representing a job attempt.
    /// https://docs.aws.amazon.com/batch/latest/APIReference/API_AttemptDetail.html
    /// </summary>
    public class AttemptDetail
    {
        /// <summary>
        /// Details about the container in this job attempt.
        /// </summary>
        public AttemptContainerDetail Container { get; set; }

        /// <summary>
        /// The Unix time stamp (in seconds and milliseconds) for when the attempt was started
        /// (when the attempt transitioned from the STARTING state to the RUNNING state).
        /// </summary>
        public long StartedAt { get; set; }

        /// <summary>
        /// A short, human-readable string to provide additional details about the current status of the job attempt.
        /// </summary>
        public string StatusReason { get; set; }

        /// <summary>
        /// The Unix time stamp (in seconds and milliseconds) for when the attempt was stopped
        /// (when the attempt transitioned from the RUNNING state to a terminal state, such as SUCCEEDED or FAILED).
        /// </summary>
        public long StoppedAt { get; set; }
    }
}