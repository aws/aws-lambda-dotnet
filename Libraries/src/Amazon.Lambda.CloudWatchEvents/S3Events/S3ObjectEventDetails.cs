namespace Amazon.Lambda.CloudWatchEvents.S3Events
{
    /// <summary>
    /// This class represents the details of an S3 object event sent via EventBridge.
    /// </summary>
    public class S3ObjectEventDetails
    {
        /// <summary>
        /// The version of the event.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// The bucket details.
        /// </summary>
        public Bucket Bucket { get; set; }

        /// <summary>
        /// The object details.
        /// </summary>
        public S3Object Object { get; set; }

        /// <summary>
        /// The ID of the API request.
        /// </summary>
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("request-id")]
#endif
        public string RequestId { get; set; }

        /// <summary>
        /// The ID of the API requester.
        /// </summary>
        public string Requester { get; set; }
    }
}
