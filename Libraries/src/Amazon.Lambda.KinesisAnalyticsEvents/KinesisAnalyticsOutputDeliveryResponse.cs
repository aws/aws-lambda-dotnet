using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.KinesisAnalyticsEvents
{
    /// <summary>
    /// This class represents the response from Lambda function for Kinesis Analytics output delivery.
    /// </summary>
    [DataContract]
    public class KinesisAnalyticsOutputDeliveryResponse
    {
        /// <summary>
        /// The record was delivered successfully.
        /// </summary>
        public const string OK = "Ok";

        /// <summary>
        /// The record delivery failed.
        /// </summary>
        public const string DELIVERY_FAILED = "DeliveryFailed";


        /// <summary>
        /// Gets or sets the records.
        /// </summary>
        /// <value>
        /// The records.
        /// </value>
        [DataMember(Name = "records")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("records")]
#endif
        public IList<Record> Records { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [DataContract]
        public class Record
        {
            /// <summary>
            /// Gets or sets the record identifier.
            /// </summary>
            /// <value>
            /// The record identifier.
            /// </value>
            [DataMember(Name = "recordId")]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("recordId")]
#endif
            public string RecordId { get; set; }

            /// <summary>
            /// Gets or sets the result.
            /// </summary>
            /// <value>
            /// The result.
            /// </value>
            [DataMember(Name = "result")]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("result")]
#endif
            public string Result { get; set; }
        }
    }
}
