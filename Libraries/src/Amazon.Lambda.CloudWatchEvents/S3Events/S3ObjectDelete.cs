namespace Amazon.Lambda.CloudWatchEvents.S3Events
{
    /// <summary>
    /// This class represents the details of an S3 object delete event sent via EventBridge.
    /// </summary>
    public class S3ObjectDelete : S3ObjectEventDetails
    {
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

        /// <summary>
        /// The type of object deletion event.
        /// </summary>
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("deletion-type")]
#endif
        public string DeletionType { get; set; }
    }
}
