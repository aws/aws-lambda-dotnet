namespace Amazon.Lambda.MQEvents
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// RabbitMQ event
    /// https://docs.aws.amazon.com/lambda/latest/dg/with-mq.html
    /// </summary>
    public class RabbitMQEvent
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
        /// List of RabbitMQ messages.
        /// </summary>
        public IDictionary<string, List<RabbitMQMessage>> RmqMessagesByQueue { get; set; }

        /// <summary>
        /// Class that represents a RabbitMQ message.
        /// </summary>
        public class RabbitMQMessage
        {
            /// <summary>
            /// Properties of RabbitMQ message
            /// </summary>
            public BasicProperties BasicProperties { get; set; }

            /// <summary>
            /// Indicates if the message was redelivered.
            /// </summary>
            public bool? Redelivered { get; set; }

            /// <summary>
            ///  Data sent to Rabbit MQ broker encoded in Base 64.
            /// </summary>
            public string Data { get; set; }
        }

        /// <summary>
        /// Class representing RabbitMQ message properties.
        /// https://rabbitmq.github.io/rabbitmq-dotnet-client/api/RabbitMQ.Client.IBasicProperties.html
        /// </summary>
        public class BasicProperties
        {
            /// <summary>
            /// Content Type
            /// </summary>
            public string ContentType { get; set; }

            /// <summary>
            /// Content Encoding
            /// </summary>
            public string ContentEncoding { get; set; }

            /// <summary>
            /// Message headers
            /// </summary>
            public IDictionary<string, object> Headers { get; set; }

            /// <summary>
            /// Delivery Mode
            /// </summary>
            public int DeliveryMode { get; set; }

            /// <summary>
            /// Priority
            /// </summary>
            public int Priority { get; set; }

            /// <summary>
            /// Correlation ID
            /// </summary>
            public string CorrelationId { get; set; }

            /// <summary>
            /// Reply To
            /// </summary>
            public string ReplyTo { get; set; }

            /// <summary>
            /// Expiration
            /// </summary>
            public string Expiration { get; set; }
            
            /// <summary>
            /// Message ID
            /// </summary>
            public string MessageId { get; set; }

            /// <summary>
            /// Timestamp
            /// </summary>
            public string Timestamp { get; set; }

            /// <summary>
            /// Message Type
            /// </summary>
            public string Type { get; set; }

            /// <summary>
            /// User ID
            /// </summary>
            public string UserId { get; set; }

            /// <summary>
            /// App ID
            /// </summary>
            public string AppId { get; set; }

            /// <summary>
            /// Cluster ID
            /// </summary>
            public string ClusterId { get; set; }

            /// <summary>
            /// Message body size
            /// </summary>
            public int BodySize { get; set; }
        }
    }
}
