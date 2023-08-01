namespace Amazon.Lambda.KinesisEvents
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// Response type to return a new state for the time window and to report batch item failures.
    /// </summary>
    [DataContract]
    public class KinesisTimeWindowResponse
    {
        /// <summary>
        /// New state after processing a batch of records.
        /// </summary>
        [DataMember(Name = "state")]
#if NETCOREAPP_3_1
        [System.Text.Json.Serialization.JsonPropertyName("state")]
#endif
        public Dictionary<String, String> State { get; set; }

        /// <summary>
        /// A list of records which failed processing.
        /// Returning the first record which failed would retry all remaining records from the batch.
        /// </summary>
        [DataMember(Name = "batchItemFailures")]
#if NETCOREAPP_3_1
        [System.Text.Json.Serialization.JsonPropertyName("batchItemFailures")]
#endif
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
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("itemIdentifier")]
#endif
            public string ItemIdentifier { get; set; }
        }
    }
}
