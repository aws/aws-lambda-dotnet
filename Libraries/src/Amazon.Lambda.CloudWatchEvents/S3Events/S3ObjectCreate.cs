using System.Runtime.Serialization;

namespace Amazon.Lambda.CloudWatchEvents.S3Events
{
    /// <summary>
    /// This class represents the details of an S3 object create event sent via EventBridge.
    /// </summary>
    [DataContract]
    public class S3ObjectCreate : S3ObjectEventDetails
    {
        /// <summary>
        /// The source IP of the API request.
        /// </summary>
        [DataMember(Name = "source-ip-address")]
#if NETCOREAPP_3_1
        [System.Text.Json.Serialization.JsonPropertyName("source-ip-address")]
#endif
        public string SourceIpAddress { get; set; }

        /// <summary>
        /// The reason the event was fired.
        /// </summary>
        [DataMember(Name = "reason")]
        public string Reason { get; set; }
    }
}
