namespace Amazon.Lambda.ScheduledEvents
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// AWS Config event
    /// http://docs.aws.amazon.com/config/latest/developerguide/evaluate-config_develop-rules.html
    /// http://docs.aws.amazon.com/config/latest/developerguide/evaluate-config_develop-rules_example-events.html
    /// https://docs.aws.amazon.com/lambda/latest/dg/eventsources.html#eventsources-scheduled-event
    /// </summary>
    public class ScheduledEvent
    {
        /// <summary>
        /// The ID of the AWS account that owns the rule.
        /// </summary>
        public string Account { get; set; }

        /// <summary>
        /// The AwsRegion in which the schedule was inovked on
        /// </summary>
        public string Region { get; set; }
        
        /// <summary>
        /// An empty object 
        /// </summary>
        public Detail Detail { get; set; }
        
        /// <summary>
        /// Static string of "Scheduled Event" or null
        /// </summary>
        public string DetailType { get; set; }
        
        /// <summary>
        /// Static string of "aws.events"
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
    
    /// <summary>
    /// The class representing the information for a Detail
    /// </summary>
    public class Detail
    {
    }
}