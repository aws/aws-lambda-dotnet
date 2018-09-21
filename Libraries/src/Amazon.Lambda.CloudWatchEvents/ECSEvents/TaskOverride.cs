using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.CloudWatchEvents.ECSEvents
{
    /// <summary>
    /// The overrides associated with a task.
    /// https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_TaskOverride.html
    /// </summary>
    public class TaskOverride
    {
        /// <summary>
        /// One or more container overrides sent to a task.
        /// </summary>
        public List<ContainerOverride> ContainerOverrides { get; set; }

        /// <summary>
        /// The Amazon Resource Name (ARN) of the task execution role that the
        /// Amazon ECS container agent and the Docker daemon can assume.
        /// </summary>
        public string ExecutionRoleArn { get; set; }

        /// <summary>
        /// The Amazon Resource Name (ARN) of the IAM role that containers in this task can assume.
        /// All containers in this task are granted the permissions that are specified in this role.
        /// </summary>
        public string TaskRoleArn { get; set; }
    }
}
