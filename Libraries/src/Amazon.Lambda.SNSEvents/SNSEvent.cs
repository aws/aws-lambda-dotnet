namespace Amazon.Lambda.SNSEvents
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Simple Notification Service event
    /// http://docs.aws.amazon.com/lambda/latest/dg/with-sns.html
    /// http://docs.aws.amazon.com/lambda/latest/dg/eventsources.html#eventsources-sns
    /// </summary>
    public class SNSEvent
    {
        /// <summary>
        /// List of SNS records.
        /// </summary>
        public IList<SNSRecord> Records { get; set; }

        /// <summary>
        /// An SNS message record.
        /// </summary>
        public class SNSRecord
        {
            /// <summary>
            /// The event source.
            /// </summary>
            public string EventSource { get; set; }

            /// <summary>
            /// The event subscription ARN.
            /// </summary>
            public string EventSubscriptionArn { get; set; }

            /// <summary>
            /// The event version.
            /// </summary>
            public string EventVersion { get; set; }

            /// <summary>
            /// The SNS message.
            /// </summary>
            public SNSMessage Sns { get; set; }
        }

        /// <summary>
        /// An SNS message record.
        /// </summary>
        public class SNSMessage
        {
            /// <summary>
            /// The message.
            /// </summary>
            public string Message { get; set; }

            /// <summary>
            /// The attributes associated with the message.
            /// </summary>
            public IDictionary<string, MessageAttribute> MessageAttributes { get; set; }

            /// <summary>
            /// The message id.
            /// </summary>
            public string MessageId { get; set; }

            /// <summary>
            /// The message signature.
            /// </summary>
            public string Signature { get; set; }

            /// <summary>
            /// The signature version used to sign the message.
            /// </summary>
            public string SignatureVersion { get; set; }

            /// <summary>
            /// The URL for the signing certificate.
            /// </summary>
            public string SigningCertUrl { get; set; }

            /// <summary>
            /// The subject for the message.
            /// </summary>
            public string Subject { get; set; }

            /// <summary>
            /// The message time stamp.
            /// </summary>
            public DateTime Timestamp { get; set; }

            /// <summary>
            /// The topic ARN.
            /// </summary>
            public string TopicArn { get; set; }

            /// <summary>
            /// The message type.
            /// </summary>
            public string Type { get; set; }

            /// <summary>
            /// The message unsubscribe URL.
            /// </summary>
            public string UnsubscribeUrl { get; set; }
        }

        /// <summary>
        /// An SNS message attribute.
        /// </summary>
        public class MessageAttribute
        {
            /// <summary>
            /// The attribute type.
            /// </summary>
            public string Type { get; set; }

            /// <summary>
            /// The attribute value.
            /// </summary>
            public string Value { get; set; }
        }
    }
}
