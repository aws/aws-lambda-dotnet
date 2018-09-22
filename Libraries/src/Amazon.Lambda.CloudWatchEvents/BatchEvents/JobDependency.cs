namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// An object representing an AWS Batch job dependency.
    /// https://docs.aws.amazon.com/batch/latest/APIReference/API_JobDependency.html
    /// </summary>
    public class JobDependency
    {
        /// <summary>
        /// The job ID of the AWS Batch job associated with this dependency.
        /// </summary>
        public string JobId { get; set; }

        /// <summary>
        /// The type of the job dependency.
        /// </summary>
        public string Type { get; set; }
    }
}