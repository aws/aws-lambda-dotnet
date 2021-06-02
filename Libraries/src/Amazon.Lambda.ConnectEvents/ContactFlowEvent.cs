using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.ConnectEvents
{
    /// <summary>
    /// This class represents the input event from Amazon Connect ContactFlow. It is used as the input parameter
    /// for Lambda functions.
    /// https://docs.aws.amazon.com/lambda/latest/dg/services-connect.html
    /// https://docs.aws.amazon.com/connect/latest/adminguide/connect-lambda-functions.html
    /// </summary>
    public class ContactFlowEvent
    {
        /// <summary>
        /// ContactFlow Details
        /// </summary>
        public ContactFlowDetails Details { get; set; }

        /// <summary>
        /// Event Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Class represnting details of ContactFlow
        /// </summary>
        public class ContactFlowDetails
        {
            /// <summary>
            /// Contact data. This is always passed by Amazon Connect for every contact.
            /// </summary>
            public ContactData ContactData { get; set; }

            /// <summary>
            /// These are parameters specific to this call that were defined when you created the Lambda function.
            /// </summary>
            public IDictionary<string, string> Parameters { get; set; }
        }

        /// <summary>
        /// Class representing contact data.
        /// </summary>
        public class ContactData
        {
            /// <summary>
            /// User attributes that have been previously associated with a contact, such as when using a Set contact attributes block in a contact flow. This map may be empty if there aren't any saved attributes.
            /// </summary>
            public IDictionary<string, string> Attributes { get; set; }

            /// <summary>
            /// Channel (such as Voice, Chat or Tasks)
            /// </summary>
            public string Channel { get; set; }

            /// <summary>
            /// Contact ID
            /// </summary>
            public string ContactId { get; set; }

            /// <summary>
            /// Customer Endpoint
            /// </summary>
            public Endpoint CustomerEndpoint { get; set; }

            /// <summary>
            /// Initial Contact ID
            /// </summary>
            public string InitialContactId { get; set; }

            /// <summary>
            /// Initiation Method (INBOUND | OUTBOUND | TRANSFER | CALLBACK)
            /// </summary>
            public string InitiationMethod { get; set; }

            /// <summary>
            /// The Amazon Resource Name (ARN) of the Amazon Connect instance.
            /// </summary>
            public string InstanceARN { get; set; }

            /// <summary>
            /// Previous Contact ID
            /// </summary>
            public string PreviousContactId { get; set; }

            /// <summary>
            /// Contains information about a queue.
            /// </summary>
            public Queue Queue { get; set; }

            /// <summary>
            /// System Endpoint
            /// </summary>
            public Endpoint SystemEndpoint { get; set; }
        }

        /// <summary>
        /// Class representing endpoint.
        /// </summary>
        public class Endpoint
        {
            /// <summary>
            /// Address (such as telephone number).
            /// </summary>
            public string Address { get; set; }

            /// <summary>
            /// Endpoint Type (such as TELEPHONE_NUMBER).
            /// </summary>
            public string Type { get; set; }
        }

        /// <summary>
        /// Contains information about a queue.
        /// </summary>
        public class Queue 
        {
            /// <summary>
            /// The Amazon Resource Name (ARN) for the queue.
            /// </summary>
            public string Arn { get; set; }

            /// <summary>
            /// Queue name.
            /// </summary>
            public string Name { get; set; }
        }
    }
}
