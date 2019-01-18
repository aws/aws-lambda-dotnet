using System;
using System.Collections.Generic;
using System.Text;

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
        /// The exit code returned from the container.
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// The last known status of the container.
        /// </summary>
        public string LastStatus { get; set; }

        /// <summary>
        /// The name of the container.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The ARN of the task.
        /// </summary>
        public string TaskArn { get; set; }
    }
}
