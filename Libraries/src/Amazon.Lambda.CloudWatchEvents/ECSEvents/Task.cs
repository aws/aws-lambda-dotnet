using System;
using System.Collections.Generic;

namespace Amazon.Lambda.CloudWatchEvents.ECSEvents
{
    /// <summary>
    /// Details on a task in a cluster.
    /// https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_Task.html
    /// </summary>
    public class Task
    {
        /// <summary>
        /// The Elastic Network Adapter associated with the task if the task uses the awsvpc network mode.
        /// </summary>
        public List<Attachment> Attachments { get; set; }

        /// <summary>
        /// The attributes of the task.
        /// </summary>
        public List<Attribute> Attributes { get; set; }

        /// <summary>
        /// The availability zone of the task.
        /// </summary>
        public string AvailabilityZone { get; set; }

        /// <summary>
        /// The capacity provider associated with the task.
        /// </summary>
        public string CapacityProviderName { get; set; }

        /// <summary>
        /// The ARN of the cluster that hosts the task.
        /// </summary>
        public string ClusterArn { get; set; }

        /// <summary>
        /// The connectivity status of a task.
        /// </summary>
        public string Connectivity { get; set; }

        /// <summary>
        /// The Unix time stamp for when the task last went into CONNECTED status.
        /// </summary>
        public DateTime ConnectivityAt { get; set; }

        /// <summary>
        /// The ARN of the container instances that host the task.
        /// </summary>
        public string ContainerInstanceArn { get; set; }

        /// <summary>
        /// The containers associated with the task.
        /// </summary>
        public List<Container> Containers { get; set; }

        /// <summary>
        /// The number of CPU units used by the task. It can be expressed as an integer using CPU units,
        /// for example 1024, or as a string using vCPUs, for example 1 vCPU or 1 vcpu, in a task definition.
        /// String values are converted to an integer indicating the CPU units when the task definition is registered.
        /// See https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_Task.html for extra info.
        /// </summary>
        public string Cpu { get; set; }

        /// <summary>
        /// The Unix time stamp for when the task was created (the task entered the PENDING state).
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// The desired status of the task. For more information,
        /// see Task Lifecycle: https://docs.aws.amazon.com/AmazonECS/latest/developerguide/task_life_cycle.html.
        /// </summary>
        public string DesiredStatus { get; set; }

        /// <summary>
        /// Whether or not execute command functionality is enabled for this task. If true, this enables execute command functionality on all containers in the task.
        /// </summary>
        public bool EnableExecuteCommand { get; set; }

        /// <summary>
        /// The ephemeral storage settings for the task.
        /// </summary>
        public EphemeralStorage EphemeralStorage { get; set; }

        /// <summary>
        /// The Unix time stamp for when the task execution stopped.
        /// </summary>
        public DateTime ExecutionStoppedAt { get; set; }

        /// <summary>
        /// The name of the task group associated with the task.
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// The health status for the task, which is determined by the health of the essential containers in the task.
        /// If all essential containers in the task are reporting as HEALTHY, then the task status also
        /// reports as HEALTHY. If any essential containers in the task are reporting as UNHEALTHY or UNKNOWN,
        /// then the task status also reports as UNHEALTHY or UNKNOWN, accordingly.
        /// See https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_Task.html for extra info.
        /// </summary>
        public string HealthStatus { get; set; }

        /// <summary>
        /// The Elastic Inference accelerator associated with the task.
        /// </summary>
        public List<InferenceAccelerator> InferenceAccelerators { get; set; }

        /// <summary>
        /// The last known status of the task. For more information,
        /// see Task Lifecycle: https://docs.aws.amazon.com/AmazonECS/latest/developerguide/task_life_cycle.html.
        /// </summary>
        public string LastStatus { get; set; }

        /// <summary>
        /// The launch type on which your task is running.
        /// </summary>
        public string LaunchType { get; set; }

        /// <summary>
        /// The amount of memory (in MiB) used by the task. It can be expressed as an integer using MiB,
        /// for example 1024, or as a string using GB, for example 1GB or 1 GB, in a task definition.
        /// String values are converted to an integer indicating the MiB when the task definition is registered.
        /// See https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_Task.html for extra info.
        /// </summary>
        public string Memory { get; set; }

        /// <summary>
        /// One or more container overrides.
        /// </summary>
        public TaskOverride Overrides { get; set; }

        /// <summary>
        /// The platform version on which your task is running. For more information,
        /// see AWS Fargate Platform Versions in the Amazon Elastic Container Service Developer Guide.
        /// https://docs.aws.amazon.com/AmazonECS/latest/developerguide/platform_versions.html
        /// </summary>
        public string PlatformVersion { get; set; }

        /// <summary>
        /// The Unix time stamp for when the container image pull began.
        /// </summary>
        public DateTime PullStartedAt { get; set; }

        /// <summary>
        /// The Unix time stamp for when the container image pull completed.
        /// </summary>
        public DateTime PullStoppedAt { get; set; }

        /// <summary>
        /// The Unix time stamp for when the task started (the task
        /// transitioned from the PENDING state to the RUNNING state).
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// The tag specified when a task is started. If the task is started by an Amazon ECS service,
        /// then the startedBy parameter contains the deployment ID of the service that starts it.
        /// </summary>
        public string StartedBy { get; set; }

        /// <summary>
        /// The stop code indicating why a task was stopped. The stoppedReason may contain additional details.
        /// </summary>
        public string StopCode { get; set; }

        /// <summary>
        /// The Unix time stamp for when the task stops (transitions from the RUNNING state to STOPPED).
        /// </summary>
        public DateTime StoppedAt { get; set; }

        /// <summary>
        /// The reason that the task was stopped.
        /// </summary>
        public string StoppedReason { get; set; }

        /// <summary>
        /// The Unix timestamp for when the task stops (transitions from the RUNNING state to STOPPED).
        /// </summary>
        public DateTime StoppingAt { get; set; }

        /// <summary>
        /// The metadata that you apply to the task to help you categorize and organize them. Each tag consists 
        /// of a key and an optional value, both of which you define. 
        /// See https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_Task.html for extra info.
        /// </summary>
        public List<KeyValuePair<string, string>> Tags { get; set; }

        /// <summary>
        /// The Amazon Resource Name (ARN) of the task.
        /// </summary>
        public string TaskArn { get; set; }

        /// <summary>
        /// The ARN of the task definition that creates the task.
        /// </summary>
        public string TaskDefinitionArn { get; set; }

        /// <summary>
        /// The version counter for the task. Every time a task experiences a change that triggers a CloudWatch event,
        /// the version counter is incremented. If you are replicating your Amazon ECS task state with
        /// CloudWatch Events, you can compare the version of a task reported by the Amazon ECS APIs with
        /// the version reported in CloudWatch Events for the task (inside the detail object) to verify that
        /// the version in your event stream is current.
        /// </summary>
        public long Version { get; set; }

        // NOTE: The UpdatedAt property is not present in the Task object documentation but has been
        // added here for convenience.

        /// <summary>
        /// The Unix time stamp for when the service was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}