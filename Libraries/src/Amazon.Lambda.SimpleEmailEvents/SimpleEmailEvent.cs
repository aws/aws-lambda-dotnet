using System;
using System.Collections.Generic;

namespace Amazon.Lambda.SimpleEmailEvents
{
    /// <summary>
    /// Simple Email Service event
    /// http://docs.aws.amazon.com/lambda/latest/dg/eventsources.html#eventsources-ses-email-receiving
    /// </summary>
    public class SimpleEmailEvent
    {
        /// <summary>
        /// List of SNS records.
        /// </summary>
        public IList<SimpleEmailRecord> Records { get; set; }

        /// <summary>
        /// An SES record.
        /// </summary>
        public class SimpleEmailRecord
        {
            /// <summary>
            /// The event version.
            /// </summary>
            public string EventVersion { get; set; }

            /// <summary>
            /// The event source.
            /// </summary>
            public string EventSource { get; set; }

            /// <summary>
            /// The SES message.
            /// </summary>
            public SimpleEmailService Ses { get; set; }
        }

        /// <summary>
        /// An SES message record.
        /// </summary>
        public class SimpleEmailService
        {
            /// <summary>
            /// The mail data for the SES message.
            /// </summary>
            public SimpleEmailMessage Mail { get; set; }

            /// <summary>
            /// The receipt data for the SES message.
            /// </summary>
            public SimpleEmailReceipt Receipt { get; set; }
        }

        /// <summary>
        /// The mail data for the SES message.
        /// </summary>
        public class SimpleEmailMessage
        {
            /// <summary>
            /// A few of the most important headers from the message.
            /// </summary>
            public SimpleEmailCommonHeaders CommonHeaders { get; set; }

            /// <summary>
            /// The source email address of the message, i.e. SMTP FROM.
            /// </summary>
            public string Source { get; set; }

            /// <summary>
            /// The timestamp of the message.
            /// </summary>
            public DateTime Timestamp { get; set; }

            /// <summary>
            /// The destination recipients of the message.
            /// </summary>
            public IList<string> Destination { get; set; }

            /// <summary>
            /// The headers associated with the message.
            /// </summary>
            public IList<SimpleEmailHeader> Headers { get; set; }

            /// <summary>
            /// Whether or not the Headers property is truncated.
            /// </summary>
            public bool HeadersTruncated { get; set; }

            /// <summary>
            /// The SES Message ID, which will also be the filename of the S3 object containing the message. Not to be confused with the incoming message's Message-ID header.
            /// </summary>
            public string MessageId { get; set; }
        }

        /// <summary>
        /// The receipt data for the SES message.
        /// </summary>
        public class SimpleEmailReceipt
        {
            /// <summary>
            /// The recipients of the message.
            /// </summary>
            public IList<string> Recipients { get; set; }

            /// <summary>
            /// The timestamp of the message.
            /// </summary>
            public DateTime Timestamp { get; set; }

            /// <summary>
            /// The spam verdict of the message, e.g. status: PASS.
            /// </summary>
            public SimpleEmailVerdict SpamVerdict { get; set; }

            /// <summary>
            /// The DKIM verdict of the message, e.g. status: PASS.
            /// </summary>
            public SimpleEmailVerdict DKIMVerdict { get; set; }

            /// <summary>
            /// The SPF verdict of the message, e.g. status: PASS.
            /// </summary>
            public SimpleEmailVerdict SPFVerdict { get; set; }

            /// <summary>
            /// The virus verdict of the message, e.g. status: PASS.
            /// </summary>
            public SimpleEmailVerdict VirusVerdict { get; set; }

            /// <summary>
            /// The virus verdict of the message, e.g. status: PASS.
            /// </summary>
            public SimpleEmailReceiptAction Action { get; set; }

            /// <summary>
            /// How long this incoming message took to process.
            /// </summary>
            public long ProcessingTimeMillis { get; set; }
        }

        /// <summary>
        /// An SES message header.
        /// </summary>
        public class SimpleEmailHeader
        {
            /// <summary>
            /// The header name.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// The header value.
            /// </summary>
            public string Value { get; set; }
        }
    }

    /// <summary>
    /// A few of the most important headers of the message.
    /// </summary>
    public class SimpleEmailCommonHeaders
    {
        /// <summary>
        /// The From header's address(es)
        /// </summary>
        public IList<string> From { get; set; }

        /// <summary>
        /// The To header's address(es)
        /// </summary>
        public IList<string> To { get; set; }

        /// <summary>
        /// The Return-Path header.
        /// </summary>
        public string ReturnPath { get; set; }

        /// <summary>
        /// The incoming message's Message-ID header. Not to be confused with the SES messageId.
        /// </summary>
        public string MessageId { get; set; }

        /// <summary>
        /// The Date header.
        /// </summary>
        public string Date { get; set; }

        /// <summary>
        /// The Subject header.
        /// </summary>
        public string Subject { get; set; }
    }

    /// <summary>
    /// The SES receipt's action.
    /// </summary>
    public class SimpleEmailReceiptAction
    {
        /// <summary>
        /// The type of the action, e.g. "Lambda"
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The type of invocation, e.g. "Event"
        /// </summary>
        public string InvocationType { get; set; }

        /// <summary>
        /// The ARN of this function.
        /// </summary>
        public string FunctionArn { get; set; }
    }

    /// <summary>
    /// Verdict to return status of Spam, DKIM, SPF, and Virus.
    /// </summary>
    public class SimpleEmailVerdict
    {
        /// <summary>
        /// The verdict status, e.g. PASS or FAIL.
        /// </summary>
        public string Status { get; set; }
    }
}