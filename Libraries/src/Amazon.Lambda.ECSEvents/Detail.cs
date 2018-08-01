﻿namespace Amazon.Lambda.ECSEvents
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// ECS event detail
    /// https://docs.aws.amazon.com/AmazonECS/latest/developerguide/ecs_cwe_events.html#ecs_container_instance_events
    /// https://docs.aws.amazon.com/AmazonECS/latest/developerguide/ecs_cwe_events.html#ecs_task_events
    /// https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_ContainerInstance.html
    /// </summary>
    public class Detail
    {
        /// <summary>
        /// This parameter returns true if the agent is connected to Amazon ECS.
        /// Registered instances with an agent that may be unhealthy or stopped return false. Instances without
        /// a connected agent can't accept placement requests.
        /// </summary>
        public bool AgentConnected { get; set; }
        
        /// <summary>
        /// The attributes set for the container instance, either by the Amazon ECS container agent at instance
        /// registration or manually with the PutAttributes operation.
        /// </summary>
        public List<ECSEvents.Attribute> Attributes { get; set; }
        
        /// <summary>
        /// The Amazon Resource Name (ARN) of the cluster that hosts the service.
        /// </summary>
        public string ClusterArn { get; set; }
        
        /// <summary>
        /// The Amazon Resource Name (ARN) of the container instance.
        /// The ARN contains the arn:aws:ecs namespace, followed by the region of the container instance,
        /// the AWS account ID of the container instance owner, the container-instance namespace,
        /// and then the container instance ID.
        /// </summary>
        public string ContainerInstanceArn { get; set; }
        
        /// <summary>
        /// List of Docker container that is part of a task.
        /// </summary>
        public List<Container> Containers { get; set; }
        
        /// <summary>
        /// The EC2 instance ID of the container instance.
        /// </summary>
        public string Ec2InstanceId { get; set; }
        
        /// <summary>
        /// For CPU and memory resource types, this parameter describes the amount of each resource that was available
        /// on the container instance when the container agent registered it with Amazon ECS; this value represents
        /// the total amount of CPU and memory that can be allocated on this container instance to tasks.
        /// For port resource types, this parameter describes the ports that were reserved by the Amazon ECS container
        /// agent when it registered the container instance with Amazon ECS.
        /// </summary>
        public List<Resource> RegisteredResources { get; set; }
        
        /// <summary>
        /// For CPU and memory resource types, this parameter describes the remaining CPU and memory that has not
        /// already been allocated to tasks and is therefore available for new tasks.
        /// For port resource types, this parameter describes the ports that were reserved by the Amazon ECS
        /// container agent (at instance registration time) and any task containers that have reserved port mappings
        /// on the host (with the host or bridge network mode).
        /// Any port that is not specified here is available for new tasks.
        /// </summary>
        public List<Resource> RemainingResources { get; set; }
        
        /// <summary>
        /// The status of the container instance.
        /// The valid values are ACTIVE, INACTIVE, or DRAINING. ACTIVE indicates that the container instance
        /// can accept tasks.
        /// DRAINING indicates that new tasks are not placed on the container instance and any service tasks
        /// running on the container instance are removed if possible.
        /// </summary>
        public string Status { get; set; }
        
        /// <summary>
        /// The version counter for the container instance.
        /// Every time a container instance experiences a change that triggers a CloudWatch event,
        /// the version counter is incremented. If you are replicating your Amazon ECS container instance state
        /// with CloudWatch Events, you can compare the version of a container instance reported by the Amazon
        /// ECS APIs with the version reported in CloudWatch Events for the container instance
        /// (inside the detail object) to verify that the version in your event stream is current.
        /// </summary>
        public int Version { get; set; }
        
        /// <summary>
        /// The version information for the Amazon ECS container agent and Docker daemon running on the container instance.
        /// </summary>
        public VersionInfo VersionInfo { get; set; }
        
        /// <summary>
        /// The Unix time stamp for when the service was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}