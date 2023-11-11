namespace Amazon.Lambda.KinesisEvents
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// Function response type to report batch item failures for KinesisEvent.
    /// https://docs.aws.amazon.com/lambda/latest/dg/with-kinesis.html#services-kinesis-batchfailurereporting
    /// </summary>
    [DataContract]
    public class StreamsEventResponse
    {
        /// <summary>
        /// A list of records which failed processing. Returning the first record which failed would retry all remaining records from the batch.
        /// </summary>
        [DataMember(Name = "batchItemFailures", EmitDefaultValue = false)]
#if NETCOREAPP3_1_OR_GREATER
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
#if NETCOREAPP3_1_OR_GREATER
            [System.Text.Json.Serialization.JsonPropertyName("itemIdentifier")]
#endif
            public string ItemIdentifier { get; set; }
        }
    }
}