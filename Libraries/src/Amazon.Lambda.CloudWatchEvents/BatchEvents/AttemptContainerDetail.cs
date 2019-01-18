using System.Collections.Generic;

namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// An object representing the details of a container that is part of a job attempt.
    /// https://docs.aws.amazon.com/batch/latest/APIReference/API_AttemptContainerDetail.html
    /// </summary>
    public class AttemptContainerDetail
    {
        /// <summary>
        /// The Amazon Resource Name (ARN) of the Amazon ECS container instance that hosts the job attempt.
        /// </summary>
        public string ContainerInstanceArn { get; set; }

        /// <summary>
        /// The exit code for the job attempt. A non-zero exit code is considered a failure.
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// The name of the CloudWatch Logs log stream associated with the container. The log group for
        /// AWS Batch jobs is /aws/batch/job. Each container attempt receives a log stream name when
        /// they reach the RUNNING status.
        /// </summary>
        public string LogStreamName { get; set; }

        /// <summary>
        /// Details about the network interfaces in this job attempt.
        /// </summary>
        public List<NetworkInterfaceDetail> NetworkInterfaces { get; set; }

        /// <summary>
        /// A short (255 max characters) human-readable string to provide additional
        /// details about a running or stopped container.
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// The Amazon Resource Name (ARN) of the Amazon ECS task that is associated with the job attempt.
        /// Each container attempt receives a task ARN when they reach the STARTING status.
        /// </summary>
        public string TaskArn { get; set; }
    }
}