namespace Amazon.Lambda.KafkaEvents
{
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Apache Kafka event
    /// https://docs.aws.amazon.com/lambda/latest/dg/with-msk.html
    /// https://docs.aws.amazon.com/lambda/latest/dg/with-kafka.html
    /// </summary>
    public class KafkaEvent
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
        /// Initial list of brokers as a CSV list of broker host or host:port.
        /// </summary>
        public string BootstrapServers { get; set; }

        /// <summary>
        /// List of Kafka event records.
        /// </summary>
        public IDictionary<string, IList<KafkaEventRecord>> Records { get; set; }

        /// <summary>
        /// Kafka event record.
        /// </summary>
        public class KafkaEventRecord
        {
            /// <summary>
            /// The topic associated with the event record.
            /// </summary>
            public string Topic { get; set; }

            /// <summary>
            /// The partition associated with the event record.
            /// </summary>
            public string Partition { get; set; }

            /// <summary>
            /// The partition offset associated with the event record.
            /// </summary>
            public long Offset { get; set; }

            /// <summary>
            /// The Kafka event record timestamp.
            /// </summary>
            public long Timestamp { get; set; }

            /// <summary>
            /// The Kafka event record timestamp type.
            /// </summary>
            public string TimestampType { get; set; }

            /// <summary>
            /// The Kafka event record Key.
            /// </summary>
            public string Key { get; set; }

            /// <summary>
            /// The Kafka event record Value.
            /// </summary>
            public MemoryStream Value { get; set; }

            /// <summary>
            /// The Kafka event record headers.
            /// </summary>
            public IList<IDictionary<string, byte[]>> Headers { get; set; }
        }
    }
}
