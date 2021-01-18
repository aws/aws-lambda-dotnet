using Amazon.Lambda.SimpleEmailEvents.Actions;
using System;
using System.Collections.Generic;

namespace Amazon.Lambda.SimpleEmailEvents
{
    /// <summary>
    /// Simple Email Service event
    /// http://docs.aws.amazon.com/lambda/latest/dg/eventsources.html#eventsources-ses-email-receiving
    /// </summary>
    public class SimpleEmailEvent<TReceiptAction> where TReceiptAction : IReceiptAction
    {
        /// <summary>
        /// List of SES records.
        /// </summary>
        public IList<SimpleEmailRecord<TReceiptAction>> Records { get; set; }

        /// <summary>
        /// An SES record.
        /// </summary>
        public class SimpleEmailRecord<TReceiptAction> where TReceiptAction : IReceiptAction
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
            public SimpleEmailService<TReceiptAction> Ses { get; set; }
        }

        /// <summary>
        /// An SES record.
        /// </summary>
        [Obsolete(
            "Please move to using SimpleEmailRecord<TReceiptAction> for greater flexibility over which type of action this record refers to. For a like for like replacement on lambda actions, please use SimpleEmailRecord<LambdaReceiptAction>"
        )]
        public class SimpleEmailRecord : SimpleEmailRecord<LambdaReceiptAction>
        { }

        /// <summary>
        /// An SES message record.
        /// </summary>
        public class SimpleEmailService<TReceiptAction> where TReceiptAction : IReceiptAction
        {
            /// <summary>
            /// The mail data for the SES message.
            /// </summary>
            public SimpleEmailMessage Mail { get; set; }

            /// <summary>
            /// The receipt data for the SES message.
            /// </summary>
            public SimpleEmailReceipt<TReceiptAction> Receipt { get; set; }
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
        /// <typeparam name="TAction">The type of action being received in this receipt</typeparam>
        public class SimpleEmailReceipt<TReceiptAction> where TReceiptAction : IReceiptAction
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
            /// The DMARC verdict of the message, e.g. status: PASS.
            /// </summary>
            public SimpleEmailVerdict DMARCVerdict { get; set; }

            /// <summary>
            /// The action of the message (i.e, which lambda was invoked, where it was stored in S3, etc)
            /// </summary>
            public TReceiptAction Action { get; set; }

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
    /// Verdict to return status of Spam, DKIM, SPF, Virus, and DMARC.
    /// </summary>
    public class SimpleEmailVerdict
    {
        /// <summary>
        /// The verdict status, e.g. PASS or FAIL.
        /// </summary>
        public string Status { get; set; }
    }

    /// <summary>
    /// Simple Email Service event
    /// http://docs.aws.amazon.com/lambda/latest/dg/eventsources.html#eventsources-ses-email-receiving
    /// </summary>
    [Obsolete(
        "Please move to using SimpleEmailEvent<TReceiptAction>, which allows greater flexibility over which type of event is being handled. For a like for like replacement if using a lambda event, use SimpleEmailEvent<LambdaReceiptAction>"
    )]
    public class SimpleEmailEvent : SimpleEmailEvent<LambdaReceiptAction>
    { }

    
}
