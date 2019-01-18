using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.CloudWatchEvents.ECSEvents
{
    /// <summary>
    /// The overrides that should be sent to a container.
    /// https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_ContainerOverride.html
    /// </summary>
    public class ContainerOverride
    {
        /// <summary>
        /// The command to send to the container that overrides the default command from
        /// the Docker image or the task definition. You must also specify a container name.
        /// </summary>
        public List<string> Command { get; set; }

        /// <summary>
        /// The number of cpu units reserved for the container, instead of the default value
        /// from the task definition. You must also specify a container name.
        /// </summary>
        public int Cpu { get; set; }

        /// <summary>
        /// The environment variables to send to the container. You can add new environment variables,
        /// which are added to the container at launch, or you can override the existing environment
        /// variables from the Docker image or the task definition. You must also specify a container name.
        /// </summary>
        public List<KeyValuePair<string, string>> Environment { get; set; }

        /// <summary>
        /// The hard limit (in MiB) of memory to present to the container, instead of the default value
        /// from the task definition. If your container attempts to exceed the memory specified here,
        /// the container is killed. You must also specify a container name.
        /// </summary>
        public int Memory { get; set; }

        /// <summary>
        /// The soft limit (in MiB) of memory to reserve for the container, instead of the default value
        /// from the task definition. You must also specify a container name.
        /// </summary>
        public int MemoryReservation { get; set; }

        /// <summary>
        /// The name of the container that receives the override.
        /// This parameter is required if any override is specified.
        /// </summary>
        public string Name { get; set; }
    }
}
