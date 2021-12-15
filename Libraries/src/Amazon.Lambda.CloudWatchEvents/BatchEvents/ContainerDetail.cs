using System.Collections.Generic;

namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// An object representing the details of a container that is part of a job.
    /// https://docs.aws.amazon.com/batch/latest/APIReference/API_ContainerDetail.html
    /// </summary>
    public class ContainerDetail
    {
        /// <summary>
        /// The command that is passed to the container.
        /// </summary>
        public List<string> Command { get; set; }

        /// <summary>
        /// The Amazon Resource Name (ARN) of the container instance on which the container is running.
        /// </summary>
        public string ContainerInstanceArn { get; set; }

        /// <summary>
        /// The environment variables to pass to a container.
        /// Note: Environment variables must not start with AWS_BATCH; this naming convention is reserved
        /// for variables that are set by the AWS Batch service.
        /// </summary>
        public List<NameValue> Environment { get; set; }

        /// <summary>
        /// The Amazon Resource Name (ARN) of the execution role that AWS Batch can assume.
        /// </summary>
        public string ExecutionRoleArn { get; set; }

        /// <summary>
        /// The exit code to return upon completion.
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// The platform configuration for jobs that are running on Fargate resources. Jobs that are running on EC2 resources must not specify this parameter.
        /// </summary>
        public FargatePlatformConfiguration FargatePlatformConfiguration { get; set; }

        /// <summary>
        /// The image used to start the container.
        /// </summary>
        public string Image { get; set; }

        /// <summary>
        /// The instance type executing the container.
        /// </summary>
        public string InstanceType { get; set; }

        /// <summary>
        /// The Amazon Resource Name (ARN) associated with the job upon execution.
        /// </summary>
        public string JobRoleArn { get; set; }

        /// <summary>
        /// Linux-specific modifications that are applied to the container, such as details for device mappings.
        /// </summary>
        public LinuxParameters LinuxParameters { get; set; }

        /// <summary>
        /// The log configuration specification for the container.
        /// </summary>
        public LogConfiguration LogConfiguration { get; set; }

        /// <summary>
        /// The name of the CloudWatch Logs log stream associated with the container.
        /// The log group for AWS Batch jobs is /aws/batch/job. Each container attempt receives a
        /// log stream name when they reach the RUNNING status.
        /// </summary>
        public string LogStreamName { get; set; }

        /// <summary>
        /// The number of MiB of memory reserved for the job.
        /// </summary>
        public int Memory { get; set; }

        /// <summary>
        /// The mount points for data volumes in your container.
        /// </summary>
        public List<MountPoint> MountPoints { get; set; }

        /// <summary>
        /// The network configuration for jobs that are running on Fargate resources. Jobs that are running on EC2 resources must not specify this parameter.
        /// </summary>
        public NetworkConfiguration NetworkConfiguration { get; set; }

        /// <summary>
        /// Details about the network interfaces in your container.
        /// </summary>
        public List<NetworkInterfaceDetail> NetworkInterfaces { get; set; }

        /// <summary>
        /// When this parameter is true, the container is given elevated privileges on the
        /// host container instance (similar to the root user).
        /// </summary>
        public bool Privileged { get; set; }

        /// <summary>
        /// When this parameter is true, the container is given read-only access to its root file system.
        /// </summary>
        public bool ReadonlyRootFilesystem { get; set; }

        /// <summary>
        /// A short (255 max characters) human-readable string to provide additional
        /// details about a running or stopped container.
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// The type and amount of resources to assign to a container. The supported resources include <c>GPU</c>, <c>MEMORY</c>, and <c>VCPU</c>.
        /// </summary>
        public List<ResourceRequirement> ResourceRequirements { get; set; }

        /// <summary>
        /// The secrets to pass to the container. For more information, see <see href="https://docs.aws.amazon.com/batch/latest/userguide/specifying-sensitive-data.html">Specifying sensitive data</see> in the <i>AWS Batch User Guide</i>.
        /// </summary>
        public List<Secret> Secrets { get; set; }

        /// <summary>
        /// The Amazon Resource Name (ARN) of the Amazon ECS task that is associated with the container job.
        /// Each container attempt receives a task ARN when they reach the STARTING status.
        /// </summary>
        public string TaskArn { get; set; }

        /// <summary>
        /// A list of ulimit values to set in the container.
        /// </summary>
        public List<Ulimit> Ulimits { get; set; }

        /// <summary>
        /// The user name to use inside the container.
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// The number of VCPUs allocated for the job.
        /// </summary>
        public int Vcpus { get; set; }

        /// <summary>
        /// A list of volumes associated with the job.
        /// </summary>
        public List<Volume> Volumes { get; set; }
    }
}