namespace Amazon.Lambda.KinesisEvents
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// AWS Kinesis stream event
    /// http://docs.aws.amazon.com/lambda/latest/dg/with-kinesis.html
    /// http://docs.aws.amazon.com/lambda/latest/dg/eventsources.html#eventsources-kinesis-streams
    /// </summary>
    public class KinesisEvent
    {
        /// <summary>
        /// List of Kinesis event records.
        /// </summary>
        public IList<KinesisEventRecord> Records { get; set; }

        /// <summary>
        /// AWS Kinesis stream record
        /// http://docs.aws.amazon.com/kinesis/latest/APIReference/API_Record.html
        /// </summary>
        public class Record : Amazon.Kinesis.Model.Record
        {
            /// <summary>
            /// The schema version for the record.
            /// </summary>
            public string KinesisSchemaVersion { get; set; }
        }

        /// <summary>
        /// Kinesis event record.
        /// </summary>
        public class KinesisEventRecord
        {
            /// <summary>
            /// The AWS region where the event originated.
            /// </summary>
            public string AwsRegion { get; set; }

            /// <summary>
            /// The event id.
            /// </summary>
            public string EventId { get; set; }

            /// <summary>
            /// The name of the event.
            /// </summary>
            public string EventName { get; set; }

            /// <summary>
            /// The source of the event.
            /// </summary>
            public string EventSource { get; set; }

            /// <summary>
            /// The ARN of the event source.
            /// </summary>
            public string EventSourceARN { get; set; }

            /// <summary>
            /// The event version.
            /// </summary>
            public string EventVersion { get; set; }

            /// <summary>
            /// The ARN for the identity used to invoke the Lambda Function.
            /// </summary>
            public string InvokeIdentityArn { get; set; }

            /// <summary>
            /// The underlying Kinesis record associated with the event.
            /// </summary>
            public Record Kinesis { get; set; }
        }
    }
}
