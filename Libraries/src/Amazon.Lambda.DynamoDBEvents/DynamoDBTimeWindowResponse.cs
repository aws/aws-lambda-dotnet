namespace Amazon.Lambda.DynamoDBEvents
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    using Amazon.Lambda.DynamoDBEvents.Converters;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Response type to return a new state for the time window and to report batch item failures.
    /// </summary>
    [DataContract]
    public class DynamoDBTimeWindowResponse
    {
        /// <summary>
        /// New state after processing a batch of records.
        /// </summary>
        [DataMember(Name = "state")]
        [System.Text.Json.Serialization.JsonPropertyName("state")]
        [JsonConverter(typeof(DictionaryLongToStringJsonConverter))]
        public Dictionary<String, String> State { get; set; }

        /// <summary>
        /// A list of records which failed processing.
        /// Returning the first record which failed would retry all remaining records from the batch.
        /// </summary>
        [DataMember(Name = "batchItemFailures")]
        [System.Text.Json.Serialization.JsonPropertyName("batchItemFailures")]
        public IList<BatchItemFailure> BatchItemFailures { get; set; }

        /// <summary>
        /// Class representing the individual record which failed processing.
        /// </summary>
        [DataContract]
        public class BatchItemFailure
        {
            /// <summary>
            /// Sequence number of the record which failed processing.
            /// </summary>
            [DataMember(Name = "itemIdentifier")]
            [System.Text.Json.Serialization.JsonPropertyName("itemIdentifier")]
            public string ItemIdentifier { get; set; }
        }
    }
}
