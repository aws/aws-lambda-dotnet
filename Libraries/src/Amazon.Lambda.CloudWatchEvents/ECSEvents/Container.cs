using System.Collections.Generic;

namespace Amazon.Lambda.CloudWatchEvents.ECSEvents
{
    /// <summary>
    /// A Docker container that is part of a task.
    /// https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_Container.html
    /// </summary>
    public class Container
    {
        /// <summary>
        /// The Amazon Resource Name (ARN) of the container.
        /// </summary>
        public string ContainerArn { get; set; }

        /// <summary>
        /// The number of CPU units set for the container. The value will be 0 if no value was specified in the container definition when the task definition was registered.
        /// </summary>
        public string Cpu { get; set; }

        /// <summary>
        /// The exit code returned from the container.
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// The IDs of each GPU assigned to the container.
        /// </summary>
        public List<string> GpuIds { get; set; }

        /// <summary>
        /// The health status of the container. If health checks are not configured for this container in its task definition, then it reports the health status as UNKNOWN.
        /// </summary>
        public string HealthStatus { get; set; }

        /// <summary>
        /// The image used for the container.
        /// </summary>
        public string Image { get; set; }

        /// <summary>
        /// The container image manifest digest.
        /// </summary>
        public string ImageDigest { get; set; }

        /// <summary>
        /// The last known status of the container.
        /// </summary>
        public string LastStatus { get; set; }

        /// <summary>
        /// The details of any Amazon ECS managed agents associated with the container.
        /// </summary>
        public List<ManagedAgent> ManagedAgents { get; set; }

        /// <summary>
        /// The hard limit (in MiB) of memory set for the container.
        /// </summary>
        public string Memory { get; set; }

        /// <summary>
        /// The soft limit (in MiB) of memory set for the container.
        /// </summary>
        public string MemoryReservation { get; set; }

        /// <summary>
        /// The name of the container.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The network bindings associated with the container.
        /// </summary>
        public List<NetworkBinding> NetworkBindings { get; set; }

        /// <summary>
        /// The network interfaces associated with the container.
        /// </summary>
        public List<NetworkInterface> NetworkInterfaces { get; set; }

        /// <summary>
        /// A short (255 max characters) human-readable string to provide additional details about a running or stopped container.
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// The ID of the Docker container.
        /// </summary>
        public string RuntimeId { get; set; }

        /// <summary>
        /// The ARN of the task.
        /// </summary>
        public string TaskArn { get; set; }
    }
}
