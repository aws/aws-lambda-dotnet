using System.Runtime.Serialization;

namespace Amazon.Lambda.CloudWatchEvents.S3Events
{
    /// <summary>
    /// This class represents the details of an S3 object delete event sent via EventBridge.
    /// </summary>
    [DataContract]
    public class S3ObjectDelete : S3ObjectEventDetails
    {
        /// <summary>
        /// The source IP of the API request.
        /// </summary>
        [DataMember(Name = "source-ip-address")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("source-ip-address")]
#endif
        public string SourceIpAddress { get; set; }

        /// <summary>
        /// The reason the event was fired.
        /// </summary>
        [DataMember(Name = "reason")]
        public string Reason { get; set; }

        /// <summary>
        /// The type of object deletion event.
        /// </summary>
        [DataMember(Name = "deletion-type")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("deletion-type")]
#endif
        public string DeletionType { get; set; }
    }
}
