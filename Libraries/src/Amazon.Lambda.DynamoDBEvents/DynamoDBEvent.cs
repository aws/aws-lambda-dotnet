namespace Amazon.Lambda.DynamoDBEvents
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// AWS DynamoDB event
    /// http://docs.aws.amazon.com/lambda/latest/dg/with-ddb.html
    /// http://docs.aws.amazon.com/lambda/latest/dg/eventsources.html#eventsources-ddb-update
    /// </summary>
    public class DynamoDBEvent
    {
        /// <summary>
        /// List of DynamoDB event records.
        /// </summary>
        public IList<DynamodbStreamRecord> Records { get; set; }

        /// <summary>
        /// DynamoDB stream record
        /// https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_streams_Record.html
        /// </summary>
        public class DynamodbStreamRecord
        {
            /// <summary>
            /// The event source arn of DynamoDB.
            /// </summary>
            public string EventSourceArn { get; set; }

            /// <summary>
            /// The region in which the <c>GetRecords</c> request was received.
            /// </summary>
            public string AwsRegion { get; set; }

            /// <summary>
            /// The main body of the stream record, containing all of the DynamoDB-specific fields.
            /// </summary>
            public StreamRecord Dynamodb { get; set; }

            /// <summary>
            /// A globally unique identifier for the event that was recorded in this stream record.
            /// </summary>
            public string EventID { get; set; }

            /// <summary>
            /// <para>
            /// The type of data modification that was performed on the DynamoDB table:
            /// </para>
            /// <ul> 
            /// <li>
            /// <para>
            ///  <c>INSERT</c> - a new item was added to the table.
            /// </para>
            /// </li>
            /// 
            /// <li>
            /// <para>
            ///  <c>MODIFY</c> - one or more of an existing item's attributes were modified.
            /// </para>
            /// </li>
            /// 
            /// <li>
            /// <para>
            ///  <c>REMOVE</c> - the item was deleted from the table
            /// </para>
            /// </li>
            /// </ul>
            /// </summary>
            public string EventName { get; set; }

            /// <summary>
            /// The Amazon Web Services service from which the stream record originated. For DynamoDB
            /// Streams, this is <c>aws:dynamodb</c>.
            /// </summary>
            public string EventSource { get; set; }

            /// <summary>
            /// <para>
            /// The version number of the stream record format. This number is updated whenever the
            /// structure of <c>Record</c> is modified.
            /// </para>
            ///  
            /// <para>
            /// Client applications must not assume that <c>eventVersion</c> will remain at
            /// a particular value, as this number is subject to change at any time. In general, <c>eventVersion</c>
            /// will only increase as the low-level DynamoDB Streams API evolves.
            /// </para>
            /// </summary>
            public string EventVersion { get; set; }

            /// <summary>
            /// <para>Items that are deleted by the Time to Live process after expiration have the following fields:</para>
            /// <ul>
            /// <li> 
            ///   <para>Records[].userIdentity.type</para>
            ///   <para>"Service"</para>
            /// </li> 
            /// <li> 
            ///   <para>Records[].userIdentity.principalId</para>
            ///   <para>"dynamodb.amazonaws.com"</para>
            /// </li> 
            /// </ul>
            /// </summary>
            public Identity UserIdentity { get; set; }
        }

        /// <summary>
        /// A description of a single data modification that was performed on an item in a DynamoDB table.
        /// </summary>
        public class StreamRecord
        {
            /// <summary>
            /// The approximate date and time when the stream record was created, in <a href="http://www.epochconverter.com/">UNIX
            /// epoch time</a> format and rounded down to the closest second.
            /// </summary>
            public DateTime ApproximateCreationDateTime { get; set; }

            /// <summary>
            /// The primary key attribute(s) for the DynamoDB item that was modified.
            /// </summary>
            public Dictionary<string, AttributeValue> Keys { get; set; }

            /// <summary>
            /// The item in the DynamoDB table as it appeared after it was modified.
            /// </summary>
            public Dictionary<string, AttributeValue> NewImage { get; set; }

            /// <summary>
            /// The item in the DynamoDB table as it appeared before it was modified.
            /// </summary>
            public Dictionary<string, AttributeValue> OldImage { get; set; }

            /// <summary>
            /// The sequence number of the stream record.
            /// </summary>
            public string SequenceNumber { get; set; }

            /// <summary>
            /// The size of the stream record, in bytes.
            /// </summary>
            public long SizeBytes { get; set; }

            /// <summary>
            /// <para>
            /// The type of data from the modified DynamoDB item that was captured in this stream record:
            /// </para>
            /// 
            /// <ul>
            /// <li>
            /// <para>
            ///  <c>KEYS_ONLY</c> - only the key attributes of the modified item.
            /// </para>
            /// </li>
            /// 
            /// <li>
            /// <para>
            ///  <c>NEW_IMAGE</c> - the entire item, as it appeared after it was modified.
            /// </para>
            /// </li>
            /// 
            /// <li>
            /// <para>
            ///  <c>OLD_IMAGE</c> - the entire item, as it appeared before it was modified.
            /// </para>
            /// </li>
            /// 
            /// <li>
            /// <para>
            ///  <c>NEW_AND_OLD_IMAGES</c> - both the new and the old item images of the item.
            /// </para>
            /// </li>
            /// </ul>
            /// </summary>
            public string StreamViewType { get; set; }
        }

        /// <summary>
        /// Contains details about the type of identity that made the request.
        /// </summary>
        public class Identity
        {
            /// <summary>
            /// A unique identifier for the entity that made the call. For Time To Live, the principalId
            /// is "dynamodb.amazonaws.com".
            /// </summary>
            public string PrincipalId { get; set; }

            /// <summary>
            /// The type of the identity. For Time To Live, the type is "Service".
            /// </summary>
            public string Type { get; set; }
        }

        /// <summary>
        /// Represents the data for an attribute.
        ///  
        /// <para>
        /// Each attribute value is described as a name-value pair. The name is the data type,
        /// and the value is the data itself.
        /// </para>
        ///  
        /// <para>
        /// For more information, see <a href="https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/HowItWorks.NamingRulesDataTypes.html#HowItWorks.DataTypes">Data
        /// Types</a> in the <i>Amazon DynamoDB Developer Guide</i>.
        /// </para>
        /// </summary>
        public class AttributeValue
        {
            /// <summary>
            /// <para>
            /// An attribute of type Binary. For example:
            /// </para>
            ///  
            /// <para>
            ///  <c>"B": "dGhpcyB0ZXh0IGlzIGJhc2U2NC1lbmNvZGVk"</c>
            /// </para>
            /// </summary>
            public MemoryStream B { get; set; }

            /// <summary>
            /// <para>
            /// An attribute of type Boolean. For example:
            /// </para>
            ///  
            /// <para>
            ///  <c>"BOOL": true</c>
            /// </para>
            /// </summary>
            public bool BOOL { get; set; }

            /// <summary>
            /// <para>
            /// An attribute of type Binary Set. For example:
            /// </para>
            ///  
            /// <para>
            ///  <c>"BS": ["U3Vubnk=", "UmFpbnk=", "U25vd3k="]</c>
            /// </para>
            /// </summary>
            public List<MemoryStream> BS { get; set; }

            /// <summary>
            /// <para>
            /// An attribute of type List. For example:
            /// </para>
            ///  
            /// <para>
            ///  <c>"L": [ {"S": "Cookies"} , {"S": "Coffee"}, {"N": "3.14159"}]</c>
            /// </para>
            /// </summary>
            public List<AttributeValue> L { get; set; }

            /// <summary>
            /// <para>
            /// An attribute of type Map. For example:
            /// </para>
            ///  
            /// <para>
            ///  <c>"M": {"Name": {"S": "Joe"}, "Age": {"N": "35"}}</c> 
            /// </para>
            /// </summary>
            public Dictionary<string, AttributeValue> M { get; set; }

            /// <summary>
            /// <para>
            /// An attribute of type Number. For example:
            /// </para>
            ///  
            /// <para>
            ///  <c>"N": "123.45"</c>
            /// </para>
            ///  
            /// <para>
            /// Numbers are sent across the network to DynamoDB as strings, to maximize compatibility
            /// across languages and libraries. However, DynamoDB treats them as number type attributes
            /// for mathematical operations.
            /// </para>
            /// </summary>
            public string N { get; set; }

            /// <summary>
            /// <para>
            /// An attribute of type Number Set. For example:
            /// </para>
            ///  
            /// <para>
            ///  <c>"NS": ["42.2", "-19", "7.5", "3.14"]</c>
            /// </para>
            ///  
            /// <para>
            /// Numbers are sent across the network to DynamoDB as strings, to maximize compatibility
            /// across languages and libraries. However, DynamoDB treats them as number type attributes
            /// for mathematical operations.
            /// </para>
            /// </summary>
            public List<string> NS { get; set; }

            /// <summary>
            /// <para>
            /// An attribute of type Null. For example:
            /// </para>
            ///  
            /// <para>
            ///  <c>"NULL": true</c> 
            /// </para>
            /// </summary>
            public bool NULL { get; set; }

            /// <summary>
            /// <para>
            /// An attribute of type String. For example:
            /// </para>
            ///  
            /// <para>
            ///  <c>"S": "Hello"</c> 
            /// </para>
            /// </summary>
            public string S { get; set; }

            /// <summary>
            /// <para>
            /// An attribute of type String Set. For example:
            /// </para>
            ///  
            /// <para>
            ///  <c>"SS": ["Giraffe", "Hippo" ,"Zebra"]</c> 
            /// </para>
            /// </summary>
            public List<string> SS { get; set; }
        }
    }
}
