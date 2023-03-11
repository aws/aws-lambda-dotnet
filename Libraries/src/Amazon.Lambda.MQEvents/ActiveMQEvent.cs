namespace Amazon.Lambda.MQEvents
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Apache ActiveMQ event
    /// https://docs.aws.amazon.com/lambda/latest/dg/with-mq.html
    /// </summary>
    public class ActiveMQEvent
    {
        /// <summary>
        /// The source of the event.
        /// </summary>
        public string EventSource { get; set; }

        /// <summary>
        /// The ARN of the event source.
        /// </summary>
        public string EventSourceArn { get; set; }

        /// <summary>
        /// List of ActiveMQ messages.
        /// </summary>
        public IList<ActiveMQMessage> Messages { get; set; }

        /// <summary>
        /// Class that represents an ActiveMQ message.
        /// </summary>
        public class ActiveMQMessage
        {
            /// <summary>
            /// Message ID
            /// </summary>
            public string MessageId { get; set; }

            /// <summary>
            /// Message Type
            /// </summary>
            public string MessageType { get; set; }

            /// <summary>
            ///  Data sent to Active MQ broker encoded in Base 64.
            /// </summary>
            public string Data { get; set; }

            /// <summary>
            /// Connection ID
            /// </summary>
            public string ConnectionId { get; set; }

            /// <summary>
            /// Indicates if the message was redelivered.
            /// </summary>
            public bool? Redelivered { get; set; }

            /// <summary>
            /// Indicates if the message is persistent.
            /// </summary>
            public bool? Persistent { get; set; }

            /// <summary>
            /// Message Destination
            /// </summary>
            public Destination Destination { get; set; }

            /// <summary>
            /// Message Timestamp
            /// </summary>
            public long? Timestamp { get; set; }

            /// <summary>
            /// Broker In Time
            /// </summary>
            public long? BrokerInTime { get; set; }

            /// <summary>
            /// Broker Out Time
            /// </summary>
            public long? BrokerOutTime { get; set; }

            /// <summary>
            /// Custom Properties
            /// </summary>
            public Dictionary<string, string> Properties { get; set; }
        }

        /// <summary>
        /// Destination queue
        /// </summary>
        public class Destination
        {
            /// <summary>
            /// Queue Name
            /// </summary>
            public string PhysicalName { get; set; }
        }
    }
}
