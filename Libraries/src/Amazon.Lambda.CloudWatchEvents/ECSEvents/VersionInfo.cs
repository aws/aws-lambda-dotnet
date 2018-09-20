using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.CloudWatchEvents.ECSEvents
{
    /// <summary>
    /// The Docker and Amazon ECS container agent version information about a container instance.
    /// https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_VersionInfo.html
    /// </summary>
    public class VersionInfo
    {
        /// <summary>
        /// The Git commit hash for the Amazon ECS container agent build on the amazon-ecs-agent GitHub repository.
        /// </summary>
        public string AgentHash { get; set; }
        /// <summary>
        /// The version number of the Amazon ECS container agent.
        /// </summary>
        public string AgentVersion { get; set; }
        /// <summary>
        /// The Docker version running on the container instance.
        /// </summary>
        public string DockerVersion { get; set; }
    }
}
