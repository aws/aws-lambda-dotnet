using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.KinesisAnalyticsEvents
{
    /// <summary>
    /// This class represents the event from Kinesis Analytics application to preprocess Kinesis Firehose data.
    /// </summary>
    [DataContract]
    public class KinesisAnalyticsFirehoseInputPreprocessingEvent
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
        public IList<FirehoseRecord> Records { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [DataContract]
        public class FirehoseRecord
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
            [DataMember(Name = "kinesisFirehoseRecordMetadata")]
#if NETCOREAPP3_1
            [System.Text.Json.Serialization.JsonPropertyName("kinesisFirehoseRecordMetadata")]
#endif
            public KinesisFirehoseRecordMetadata RecordMetadata { get; set; }

            /// <summary>
            /// 
            /// </summary>
            [DataContract]
            public class KinesisFirehoseRecordMetadata
            {
                /// <summary>
                /// The approximate time the record was sent to Kinesis Firehose.
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
                /// The approximate time the record was sent to Kinesis Firehose in epoch.
                /// </summary>
                [DataMember(Name = "approximateArrivalTimestamp")]
#if NETCOREAPP3_1
                [System.Text.Json.Serialization.JsonPropertyName("approximateArrivalTimestamp")]
#endif
                public long ApproximateArrivalEpoch { get; set; }

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
