using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.KinesisAnalyticsEvents
{
    /// <summary>
    /// This class represents the event from Kinesis Analytics application output.
    /// </summary>
    [DataContract]
    public class KinesisAnalyticsOutputDeliveryEvent
    {

        /// <summary>
        /// Gets or sets the invocation identifier.
        /// </summary>
        /// <value>
        /// The invocation identifier.
        /// </value>
        [DataMember(Name = "invocationId")]
        public string InvocationId { get; set; }

        /// <summary>
        /// Gets or sets the application arn.
        /// </summary>
        /// <value>
        /// The application arn.
        /// </value>
        [DataMember(Name = "applicationArn")]
        public string ApplicationArn { get; set; }

        /// <summary>
        /// Gets or sets the records.
        /// </summary>
        /// <value>
        /// The records.
        /// </value>
        [DataMember(Name = "records")]
        public IList<DeliveryRecord> Records { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [DataContract]
        public class DeliveryRecord
        {
            /// <summary>
            /// Gets or sets the record identifier.
            /// </summary>
            /// <value>
            /// The record identifier.
            /// </value>
            [DataMember(Name = "recordId")]
            public string RecordId { get; set; }

            /// <summary>
            /// Gets or sets the record metadata.
            /// </summary>
            /// <value>
            /// The record metadata.
            /// </value>
            [DataMember(Name = "lambdaDeliveryRecordMetadata")]
            public LambdaDeliveryRecordMetadata RecordMetadata { get; set; }

            /// <summary>
            /// 
            /// </summary>
            [DataContract]
            public class LambdaDeliveryRecordMetadata
            {
                /// <summary>
                /// Gets or sets the retry hint.
                /// </summary>
                /// <value>
                /// The retryHint is a value that increases for every delivery failure of the record.
                /// </value>
                [DataMember(Name = "retryHint")]
                public long RetryHint { get; set; }
            }

            /// <summary>
            /// Gets or sets the base64 encoded data.
            /// </summary>
            /// <value>
            /// The base64 encoded data.
            /// </value>
            [DataMember(Name = "data")]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("data")]
#endif
            public string Base64EncodedData { get; set; }

            /// <summary>
            /// Base64 decodes the Base64EncodedData property.
            /// </summary>
            /// <returns></returns>
            public string DecodeData()
            {
                var decodedData = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(this.Base64EncodedData));
                return decodedData;
            }
        }
    }
}