namespace Amazon.Lambda.DynamoDBEvents
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// This class is used as the return type for AWS Lambda functions that are invoked by DynamoDB to report batch item failures.
    /// https://docs.aws.amazon.com/lambda/latest/dg/with-ddb.html#services-ddb-batchfailurereporting
    /// </summary>
    [DataContract]
    public class StreamsEventResponse
    {
        /// <summary>
        /// A list of records which failed processing. Returning the first record which failed would retry all remaining records from the batch.
        /// </summary>
        [DataMember(Name = "batchItemFailures", EmitDefaultValue = false)]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("batchItemFailures")]
#endif
        public IList<BatchItemFailure> BatchItemFailures { get; set; }

        /// <summary>
        /// The class representing the BatchItemFailure.
        /// </summary>
        [DataContract]
        public class BatchItemFailure
        {
            /// <summary>
            /// Sequence number of the record which failed processing.
            /// </summary>
            [DataMember(Name = "itemIdentifier", EmitDefaultValue = false)]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("itemIdentifier")]
#endif
            public string ItemIdentifier { get; set; }
        }
    }
}