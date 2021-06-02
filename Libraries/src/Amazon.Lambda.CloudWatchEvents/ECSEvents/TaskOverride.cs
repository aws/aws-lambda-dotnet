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
        /// The cpu override for the task.
        /// </summary>
        public string Cpu { get; set; }

        /// <summary>
        /// The ephemeral storage setting override for the task.
        /// </summary>
        public EphemeralStorage EphemeralStorage { get; set; }

        /// <summary>
        /// The Amazon Resource Name (ARN) of the task execution IAM role override for the task.
        /// </summary>
        public string ExecutionRoleArn { get; set; }

        /// <summary>
        /// The Elastic Inference accelerator override for the task.
        /// </summary>
        public List<InferenceAcceleratorOverride> InferenceAcceleratorOverrides { get; set; }

        /// <summary>
        /// The memory override for the task.
        /// </summary>
        public string Memory { get; set; }

        /// <summary>
        /// The Amazon Resource Name (ARN) of the IAM role that containers in this task can assume.
        /// All containers in this task are granted the permissions that are specified in this role.
        /// </summary>
        public string TaskRoleArn { get; set; }
    }
}
