using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Amazon.Lambda.KinesisFirehoseEvents
{
    /// <summary>
    /// This class represents the input event from Amazon Kinesis Firehose. It used as the input parameter
    /// for Lambda functions.
    /// </summary>
    public class KinesisFirehoseEvent
    {
        /// <summary>
        /// The Id of the invocation.
        /// </summary>
        public string InvocationId { get; set; }

        /// <summary>
        /// The ARN of the delivery stream sending the event.
        /// </summary>
        public string DeliveryStreamArn { get; set; }

        /// <summary>
        /// The AWS region for delivery stream.
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// The Kinesis records to transform.
        /// </summary>
        public IList<FirehoseRecord> Records { get; set; }

        /// <summary>
        /// The records for the Kinesis Firehose event to process and transform.
        /// </summary>
        [DataContract]
        public class FirehoseRecord
        {
            /// <summary>
            ///The record ID is passed from Firehose to Lambda during the invocation. The transformed record must 
            ///contain the same record ID. Any mismatch between the ID of the original record and the ID of the 
            ///transformed record is treated as a data transformation failure.
            /// </summary>
            [DataMember(Name = "recordId")]
            public string RecordId { get; set; }


            /// <summary>
            /// The approximate time the record was sent to Kinesis Firehose as a Unix epoch.
            /// </summary>
            [DataMember(Name = "approximateArrivalTimestamp")]
#if NETCOREAPP_3_1        
            [System.Text.Json.Serialization.JsonPropertyName("approximateArrivalTimestamp")]
#endif
            public long ApproximateArrivalEpoch { get; set; }

            /// <summary>
            /// The approximate time the record was sent to Kinesis Firehose.
            /// </summary>
            [IgnoreDataMember]
#if NETCOREAPP_3_1        
            [System.Text.Json.Serialization.JsonIgnore()]
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
            /// The data sent through as a Kinesis Firehose record. The data is sent to the Lambda function base64 encoded.
            /// </summary>
            [DataMember(Name = "data")]
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("data")]
#endif
            public string Base64EncodedData { get; set; }

            /// <summary>
            /// Base64 decodes the Base64EncodedData property.
            /// </summary>
            /// <returns></returns>
            public string DecodeData()
            {
                var decodedData = Encoding.UTF8.GetString(Convert.FromBase64String(this.Base64EncodedData));
                return decodedData;
            }
        }

    }
}
