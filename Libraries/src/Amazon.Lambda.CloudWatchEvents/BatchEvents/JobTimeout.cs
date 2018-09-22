namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// An object representing a job timeout configuration.
    /// https://docs.aws.amazon.com/batch/latest/APIReference/API_JobTimeout.html
    /// </summary>
    public class JobTimeout
    {
        /// <summary>
        /// The time duration in seconds (measured from the job attempt's startedAt timestamp) after which
        /// AWS Batch terminates your jobs if they have not finished.
        /// </summary>
        public int AttemptDurationSeconds { get; set; }
    }
}