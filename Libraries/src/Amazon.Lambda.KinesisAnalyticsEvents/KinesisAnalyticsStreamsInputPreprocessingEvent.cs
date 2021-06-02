using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.KinesisAnalyticsEvents
{
    /// <summary>
    /// This class represents the event from Kinesis Analytics application to preprocess Kinesis stream data.
    /// </summary>
    [DataContract]
    public class KinesisAnalyticsStreamsInputPreprocessingEvent
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
        /// Gets or sets the stream arn.
        /// </summary>
        /// <value>
        /// The stream arn.
        /// </value>
        [DataMember(Name = "streamArn")]
        public string StreamArn { get; set; }

        /// <summary>
        /// Gets or sets the records.
        /// </summary>
        /// <value>
        /// The records.
        /// </value>
        [DataMember(Name = "records")]
        public IList<StreamsRecord> Records { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [DataContract]
        public class StreamsRecord
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
            [DataMember(Name = "kinesisStreamRecordMetadata")]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("kinesisStreamRecordMetadata")]
#endif
            public KinesisStreamRecordMetadata RecordMetadata { get; set; }

            /// <summary>
            /// 
            /// </summary>
            [DataContract]
            public class KinesisStreamRecordMetadata
            {
                /// <summary>
                /// Gets or sets the sequence number.
                /// </summary>
                /// <value>
                /// The sequence number.
                /// </value>
                [DataMember(Name = "sequenceNumber")]
                public string SequenceNumber { get; set; }

                /// <summary>
                /// Gets or sets the partition key.
                /// </summary>
                /// <value>
                /// The partition key.
                /// </value>
                [DataMember(Name = "partitionKey")]
                public string PartitionKey { get; set; }

                /// <summary>
                /// The approximate time the record was sent to Kinesis Steam.
                /// </summary>
                [IgnoreDataMember]
#if NETCOREAPP3_1
                [System.Text.Json.Serialization.JsonIgnore]
#endif
                public DateTime ApproximateArrivalTimestamp
                {
                    get
                    {
                        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        return epoch.AddMilliseconds(ApproximateArrivalEpoch);
                    }
                }

                /// <summary>
                /// The approximate time the record was sent to Kinesis stream in epoch.
                /// </summary>
                [DataMember(Name = "approximateArrivalTimestamp")]
#if NETCOREAPP3_1
                [System.Text.Json.Serialization.JsonPropertyName("approximateArrivalTimestamp")]
#endif
                public long ApproximateArrivalEpoch { get; set; }

                /// <summary>
                /// Gets or sets the shard identifier.
                /// </summary>
                /// <value>
                /// The shard identifier.
                /// </value>
                [DataMember(Name = "shardId")]
                public string ShardId { get; set; }

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
