namespace Amazon.Lambda.SQSEvents
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Simple Queue Service event
    /// </summary>
    public class SQSEvent
    {

        /// <summary>
        /// Get and sets the Records
        /// </summary>
        public List<SQSMessage> Records { get; set; }

        /// <summary>
        /// Class containing the data for message attributes
        /// </summary>
        public class MessageAttribute
        {
            /// <summary>
            /// Get and sets value of message attribute of type String or type Number
            /// </summary>
            public string StringValue { get; set; }

            /// <summary>
            /// Get and sets value of message attribute of type Binary
            /// </summary>
            public MemoryStream BinaryValue { get; set; }

            /// <summary>
            /// Get and sets the list of String values of message attribute
            /// </summary>
            public List<string> StringListValues { get; set; }

            /// <summary>
            /// Get and sets the list of Binary values of message attribute
            /// </summary>
            public List<MemoryStream> BinaryListValues { get; set; }

            /// <summary>
            /// Get and sets the dataType of message attribute
            /// </summary>
            public string DataType { get; set; }
        }

        /// <summary>
        /// Class representing a SQS message event coming into a Lambda function
        /// </summary>
        public class SQSMessage
        {

            /// <summary>
            /// Get and sets the message id
            /// </summary>
            public string MessageId { get; set; }

            /// <summary>
            /// Get and sets the receipt handle
            /// </summary>
            public string ReceiptHandle { get; set; }

            /// <summary>
            /// Get and sets the Body
            /// </summary>
            public string Body { get; set; }

            /// <summary>
            /// Get and sets the Md5OfBody
            /// </summary>
            public string Md5OfBody { get; set; }

            /// <summary>
            /// Get and sets the Md5OfMessageAttributes
            /// </summary>
            public string Md5OfMessageAttributes { get; set; }

            /// <summary>
            /// Get and sets the EventSourceArn
            /// </summary>
            public string EventSourceArn { get; set; }

            /// <summary>
            /// Get and sets the EventSource
            /// </summary>
            public string EventSource { get; set; }

            /// <summary>
            /// Get and sets the AwsRegion
            /// </summary>
            public string AwsRegion { get; set; }

            /// <summary>
            /// Get and sets the Attributes
            /// </summary>
            public Dictionary<string, string> Attributes { get; set; }

            /// <summary>
            /// Get and sets the MessageAttributes
            /// </summary>
            public Dictionary<string, MessageAttribute> MessageAttributes { get; set; }
        }
    }
}
