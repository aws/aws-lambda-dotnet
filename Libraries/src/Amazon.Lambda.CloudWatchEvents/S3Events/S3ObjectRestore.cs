using System.Runtime.Serialization;

namespace Amazon.Lambda.CloudWatchEvents.S3Events
{
    /// <summary>
    /// This class represents the details of an S3 object restore event sent via EventBridge.
    /// </summary>
    [DataContract]
    public class S3ObjectRestore : S3ObjectEventDetails
    {
        /// <summary>
        /// The time when the temporary copy of the object will be deleted from S3.
        /// </summary>
        [DataMember(Name = "restore-expiry-time")]
        [System.Text.Json.Serialization.JsonPropertyName("restore-expiry-time")]
        public string RestoreExpiryTime { get; set; }

        /// <summary>
        /// The storage class of the object being restored.
        /// </summary>
        [DataMember(Name = "source-storage-class")]
        [System.Text.Json.Serialization.JsonPropertyName("source-storage-class")]
        public string SourceStorageClass { get; set; }
    }
}
