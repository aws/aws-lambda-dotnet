namespace Amazon.Lambda.CloudWatchEvents
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// AWS CloudWatch event
    /// The contents of the detail top-level field are different depending on which service generated the event and what the event is.
    /// The combination of the source and detail-type fields serves to identify the fields and values found in the detail field.
    /// Complete list of events that inherit this interface: https://docs.aws.amazon.com/AmazonCloudWatch/latest/events/EventTypes.html
    /// https://docs.aws.amazon.com/AmazonCloudWatch/latest/events/CloudWatchEventsandEventPatterns.html
    /// https://docs.aws.amazon.com/AmazonCloudWatch/latest/events/EventTypes.html
    /// </summary>
    public class CloudWatchEvent<T>
    {
        /// <summary>
        /// By default, this is set to 0 (zero) in all events.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// The 12-digit number identifying an AWS account.
        /// </summary>
        public string Account { get; set; }

        /// <summary>
        /// Identifies the AWS region where the event originated.
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// A JSON object, whose content is at the discretion of the service originating the event.
        /// The detail content in the example above is very simple, just two fields.
        /// AWS API call events have detail objects with around 50 fields nested several levels deep.
        /// </summary>
        public T Detail { get; set; }

        /// <summary>
        /// Identifies, in combination with the source field, the fields and values that appear in the detail field.
        /// For example, ScheduledEvent will be null
        /// For example, ECSEvent could be "ECS Container Instance State Change" or "ECS Task State Change"
        /// </summary>
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("detail-type")]
#endif
        public string DetailType { get; set; }

        /// <summary>
        /// Identifies the service that sourced the event.
        /// All events sourced from within AWS begin with "aws."
        /// Customer-generated events can have any value here, as long as it doesn't begin with "aws."
        /// We recommend the use of Java package-name style reverse domain-name strings.
        /// For example, ScheduledEvent will be "aws.events"
        /// For example, ECSEvent will be "aws.ecs"
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// The event timestamp, which can be specified by the service originating the event.
        /// If the event spans a time interval, the service might choose to report the start time,
        /// so this value can be noticeably before the time the event is actually received.
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// A unique value is generated for every event.
        /// This can be helpful in tracing events as they move through rules to targets, and are processed.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// This JSON array contains ARNs that identify resources that are involved in the event.
        /// Inclusion of these ARNs is at the discretion of the service.
        /// For example, Amazon EC2 instance state-changes include Amazon EC2 instance ARNs, Auto Scaling events
        /// include ARNs for both instances and Auto Scaling groups, but API calls with AWS CloudTrail do not
        /// include resource ARNs.
        /// </summary>
        public List<string> Resources { get; set; }
    }
}
