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

        /// <summary>
        /// The source IP of the API request.
        /// </summary>
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("source-ip-address")]
#endif
        public string SourceIpAddress { get; set; }

        /// <summary>
        /// The reason the event was fired.
        /// </summary>
        public string Reason { get; set; }
    }
}
