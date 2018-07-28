namespace Amazon.Lambda.ScheduledEvents
{
    using System;
    using System.Collections.Generic;
    using CloudWatchEvents;    

    /// <summary>
    /// AWS Scheduled event
    /// http://docs.aws.amazon.com/config/latest/developerguide/evaluate-config_develop-rules.html
    /// http://docs.aws.amazon.com/config/latest/developerguide/evaluate-config_develop-rules_example-events.html
    /// https://docs.aws.amazon.com/lambda/latest/dg/eventsources.html#eventsources-scheduled-event
    /// </summary>
    public class ScheduledEvent : ICloudWatchEvent<Detail>
    {
        /// <inheritdoc />
        public string Version { get; set; }

        /// <inheritdoc />
        public string Account { get; set; }

        /// <inheritdoc />
        public string Region { get; set; }

        /// <summary>
        /// An empty object
        /// </summary>
        public Detail Detail { get; set; }

        /// <summary>
        /// A static string of null
        /// </summary>
        public string DetailType { get; set; }

        /// <summary>
        /// A static string of "aws.events"
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