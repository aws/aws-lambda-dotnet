using System.Collections.Generic;

namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/batch/latest/userguide/batch_cwe_events.html
    /// https://docs.aws.amazon.com/batch/latest/APIReference/API_JobDetail.html
    /// https://docs.aws.amazon.com/batch/latest/APIReference/API_DescribeJobs.html
    /// </summary>
    public class Job
    {
        /// <summary>
        /// The array properties of the job, if it is an array job.
        /// </summary>
        public ArrayPropertiesDetail ArrayProperties { get; set; }

        /// <summary>
        /// A list of job attempts associated with this job.
        /// </summary>
        public List<AttemptDetail> Attempts { get; set; }

        /// <summary>
        /// An object representing the details of the container that is associated with the job.
        /// </summary>
        public ContainerDetail Container { get; set; }

        /// <summary>
        /// The Unix time stamp (in seconds and milliseconds) for when the job was created. For non-array
        /// jobs and parent array jobs, this is when the job entered the SUBMITTED state
        /// (at the time SubmitJob was called). For array child jobs, this is when the child job was
        /// spawned by its parent and entered the PENDING state.
        /// </summary>
        public long CreatedAt { get; set; }

        /// <summary>
        /// A list of job names or IDs on which this job depends.
        /// </summary>
        public List<JobDependency> DependsOn { get; set; }

        /// <summary>
        /// The job definition that is used by this job.
        /// </summary>
        public string JobDefinition { get; set; }

        /// <summary>
        /// The ID for the job.
        /// </summary>
        public string JobId { get; set; }

        /// <summary>
        /// The name of the job.
        /// </summary>
        public string JobName { get; set; }

        /// <summary>
        /// The Amazon Resource Name (ARN) of the job queue with which the job is associated.
        /// </summary>
        public string JobQueue { get; set; }

        /// <summary>
        /// An object representing the details of a node that is associated with a multi-node
        /// parallel job.
        /// </summary>
        public NodeDetails NodeDetails { get; set; }

        /// <summary>
        /// An object representing the node properties of a multi-node parallel job.
        /// </summary>
        public NodeProperties NodeProperties { get; set; }

        /// <summary>
        /// Additional parameters passed to the job that replace parameter substitution placeholders or
        /// override any corresponding parameter defaults from the job definition.
        /// </summary>
        public Dictionary<string, string> Parameters { get; set; }

        /// <summary>
        /// The retry strategy to use for this job if an attempt fails.
        /// </summary>
        public RetryStrategy RetryStrategy { get; set; }

        /// <summary>
        /// The Unix time stamp (in seconds and milliseconds) for when the job was started (when the job
        /// transitioned from the STARTING state to the RUNNING state).
        /// </summary>
        public long StartedAt { get; set; }

        /// <summary>
        /// The current status for the job. Note: If your jobs do not progress to STARTING, see Jobs Stuck
        /// in RUNNABLE Status in the troubleshooting section of the AWS Batch User Guide.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// A short, human-readable string to provide additional details about the current status of the job.
        /// </summary>
        public string StatusReason { get; set; }

        /// <summary>
        /// The Unix time stamp (in seconds and milliseconds) for when the job was stopped (when the
        /// job transitioned from the RUNNING state to a terminal state, such as SUCCEEDED or FAILED).
        /// </summary>
        public long StoppedAt { get; set; }

        /// <summary>
        /// The timeout configuration for the job.
        /// </summary>
        public JobTimeout Timeout { get; set; }
    }
}