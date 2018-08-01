namespace Amazon.Lambda.ECSEvents
{
    using System;
    using System.Collections.Generic;
    using CloudWatchEvents;

    /// <summary>
    /// AWS ECS event
    /// http://docs.aws.amazon.com/config/latest/developerguide/evaluate-config_develop-rules.html
    /// http://docs.aws.amazon.com/config/latest/developerguide/evaluate-config_develop-rules_example-events.html
    /// https://docs.aws.amazon.com/AmazonECS/latest/developerguide/ecs_cwe_events.html
    /// </summary>
    public class ECSEvent : ICloudWatchEvent<Detail>
    {
        /// <inheritdoc />
        /// The version in the detail object of the event describes the version of the associated resource.
        /// Each time a resource changes state, this version is incremented.
        /// Because events can be sent multiple times, this field allows you to identify duplicate events
        /// (they will have the same version in the detail object).
        /// If you are replicating your Amazon ECS container instance and task state with CloudWatch events,
        /// you can compare the version of a resource reported by the Amazon ECS APIs with the version reported
        /// in CloudWatch events for the resource (inside the detail object) to verify that the version in your
        /// event stream is current.
        public string Version { get; set; }

        /// <inheritdoc />
        public string Account { get; set; }

        /// <inheritdoc />
        public string Region { get; set; }

        /// <summary>
        /// Detail resembles the Task object that is returned from a DescribeTasks API operation in the
        /// Amazon Elastic Container Service API Reference
        /// </summary>
        public Detail Detail { get; set; }

        /// <summary>
        /// A string of "ECS Container Instance State Change" or "ECS Task State Change"
        /// </summary>
        public string DetailType { get; set; }

        /// <summary>
        /// A static string of "aws.ecs"
        /// </summary>
        public string Source { get; set; }

        /// <inheritdoc />
        public DateTime Time { get; set; }

        /// <inheritdoc />
        public string Id { get; set; }

        /// <inheritdoc />
        public List<string> Resources { get; set; }
    }
}