using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Amazon.Lambda.KinesisFirehoseEvents
{

    /// <summary>
    /// The response for the Lambda functions handling Kinesis Firehose transformations.
    /// </summary>
    [DataContract]
    public class KinesisFirehoseResponse
    {
        /// <summary>
        /// The record was transformed successfully.
        /// </summary>
        public const string TRANSFORMED_STATE_OK = "Ok";

        /// <summary>
        /// The record was dropped intentionally by your processing logic.
        /// </summary>
        public const string TRANSFORMED_STATE_DROPPED = "Dropped";

        /// <summary>
        /// The record could not be transformed.
        /// </summary>
        public const string TRANSFORMED_STATE_PROCESSINGFAILED = "ProcessingFailed";

        /// <summary>
        /// The transformed records from the KinesisFirehoseEvent.
        /// </summary>        
        [DataMember(Name = "records")]
#if NETCOREAPP_3_1
        [System.Text.Json.Serialization.JsonPropertyName("records")]
#endif
        public IList<FirehoseRecord> Records { get; set; }

        /// <summary>
        /// The transformed records after processing KinesisFirehoseEvent.Records
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
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("recordId")]
#endif
            public string RecordId { get; set; }

            /// <summary>
            /// The status of the data transformation of the record. The possible values are: "Ok" 
            /// (the record was transformed successfully), "Dropped" (the record was dropped intentionally 
            /// by your processing logic), and "ProcessingFailed" (the record could not be transformed). 
            /// If a record has a status of "Ok" or "Dropped", Firehose considers it successfully 
            /// processed. Otherwise, Firehose considers it unsuccessfully processed.
            /// 
            /// Possible values:
            /// <list type="table">
            ///     <item>
            ///         <term>Ok</term>
            ///         <description>The record was transformed successfully</description>
            ///     </item>
            ///     <item>
            ///         <term>Dropped</term>
            ///         <description>The record was dropped intentionally by your processing logic</description>
            ///     </item>
            ///     <item>
            ///         <term>ProcessingFailed</term>
            ///         <description>The record could not be transformed</description>
            ///     </item>
            /// </list>
            /// </summary>
            [DataMember(Name = "result")]
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("result")]
#endif
            public string Result { get; set; }

            /// <summary>
            /// The transformed data payload, after base64-encoding.
            /// </summary>
            [DataMember(Name = "data")]
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("data")]
#endif
            public string Base64EncodedData { get; set; }

            /// <summary>
            /// Base64 encodes the data and sets the Base64EncodedData property.
            /// </summary>
            /// <param name="data">The data to be base64 encoded.</param>
            public void EncodeData(string data)
            {
                this.Base64EncodedData = Convert.ToBase64String(Encoding.UTF8.GetBytes(data));
            }
        }
    }
}
