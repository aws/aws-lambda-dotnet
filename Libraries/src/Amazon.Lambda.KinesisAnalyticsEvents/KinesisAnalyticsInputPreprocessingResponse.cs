using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.KinesisAnalyticsEvents
{
    /// <summary>
    /// This class represents the response from Lambda function for Kinesis Analytics input preprocessing.
    /// </summary>
    [DataContract]
    public class KinesisAnalyticsInputPreprocessingResponse
    {
        /// <summary>
        /// The record was preprocessed successfully.
        /// </summary>
        public const string OK = "Ok";

        /// <summary>
        /// The record was dropped intentionally by your processing logic.
        /// </summary>
        public const string DROPPED = "Dropped";

        /// <summary>
        /// The record could not be preprocessed.
        /// </summary>
        public const string PROCESSINGFAILED = "ProcessingFailed";


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
            /// Encodes the data.
            /// </summary>
            /// <param name="data">The data.</param>
            public void EncodeData(string data)
            {
                this.Base64EncodedData = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(data));
            }
        }
    }
}
