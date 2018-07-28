namespace Amazon.Lambda.ScheduledEvents
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// AWS Scheduled event
    /// http://docs.aws.amazon.com/config/latest/developerguide/evaluate-config_develop-rules.html
    /// http://docs.aws.amazon.com/config/latest/developerguide/evaluate-config_develop-rules_example-events.html
    /// https://docs.aws.amazon.com/lambda/latest/dg/eventsources.html#eventsources-scheduled-event
    /// </summary>
    public class ScheduledEvent<T>
    {
        /// <summary>
        /// The version of the event based on trigger source
        /// </summary>
        public string Version { get; set; }
        
        /// <summary>
        /// The ID of the AWS account that owns the rule
        /// </summary>
        public string Account { get; set; }

        /// <summary>
        /// The AwsRegion in which the schedule was inovked on
        /// </summary>
        public string Region { get; set; }
        
        /// <summary>
        /// A custom object based on trigger source.
        /// Example: CloudWatch rule will result in an empty object
        /// Example: ECS Task State Change will result in a rather large object
        /// </summary>
        public T Detail { get; set; }
        
        /// <summary>
        /// A string description of the detail object.
        /// Example: CloudWatch will be null
        /// Example: ECS Task state change will be "ECS Task State Change"
        /// </summary>
        public string DetailType { get; set; }
        
        /// <summary>
        /// The source of the invoking scheduled event
        /// Example: CloudWatch will be "aws.events"
        /// Example: ECS Task state change will be "aws.ecs"
        /// </summary>
        public string Source { get; set; }
        
        /// <summary>
        /// The event time stamp. 
        /// </summary>
        public DateTime Time { get; set; }
        
        /// <summary>
        /// The schedule Id 
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// The resource of the invoking schedule 
        /// </summary>
        public List<string> Resources { get; set; }
    }
}