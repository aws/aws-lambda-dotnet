using System.Runtime.Serialization;

namespace Amazon.Lambda.CloudWatchEvents.S3Events
{
    /// <summary>
    /// This class represents the details of an S3 object event sent via EventBridge.
    /// </summary>
    [DataContract]
    public class S3ObjectEventDetails
    {
        /// <summary>
        /// The version of the event.
        /// </summary>
        [DataMember(Name = "version")]
        public string Version { get; set; }

        /// <summary>
        /// The bucket details.
        /// </summary>
        [DataMember(Name = "bucket")]
        public Bucket Bucket { get; set; }

        /// <summary>
        /// The object details.
        /// </summary>
        [DataMember(Name = "object")]
        public S3Object Object { get; set; }

        /// <summary>
        /// The ID of the API request.
        /// </summary>
        [DataMember(Name = "request-id")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("request-id")]
#endif
        public string RequestId { get; set; }

        /// <summary>
        /// The ID of the API requester.
        /// </summary>
        [DataMember(Name = "requester")]
        public string Requester { get; set; }
    }
}
