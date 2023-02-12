using System.Runtime.Serialization;

namespace Amazon.Lambda.CloudWatchEvents.S3Events
{
    /// <summary>
    /// This class represents an S3 object.
    /// </summary>
    [DataContract]
    public class S3Object
    {
        /// <summary>
        /// The key for the object stored in S3.
        /// </summary>
        [DataMember(Name = "key")]
        public string Key { get; set; }

        /// <summary>
        /// The size of the object.
        /// </summary>
        [DataMember(Name = "size")]
        public long Size { get; set; }

        /// <summary>
        /// The etag of the object.
        /// </summary>
        [DataMember(Name = "etag")]
        public string ETag { get; set; }

        /// <summary>
        /// The version ID of the object.
        /// </summary>
        [DataMember(Name = "version-id")]
#if NETCOREAPP_3_1
        [System.Text.Json.Serialization.JsonPropertyName("version-id")]
#endif
        public string VersionId { get; set; }

        /// <summary>
        /// A string used to determine event sequence in PUTs and DELETEs.
        /// </summary>
        [DataMember(Name = "sequencer")]
        public string Sequencer { get; set; }
    }
}
