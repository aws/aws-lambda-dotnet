namespace Amazon.Lambda.AspNetCoreServer.Hosting
{
    /// <summary>
    /// Options for configuring AWS Lambda content type transformation
    /// </summary>
    public class EncodingOptions : IEncodingOptions
    {
        /// <summary>
        /// Defines a mapping from registered content types to the response encoding format 
        /// which dictates what transformations should be applied before returning response content
        /// </summary>
        public Dictionary<string, ResponseContentEncoding>? ResponseContentEncodingForContentType { get; set ; }

        /// <summary>
        /// Defines a mapping from registered content encodings to the response encoding format
        /// which dictates what transformations should be applied before returning response content
        /// </summary>
        public Dictionary<string, ResponseContentEncoding>? ResponseContentEncodingForContentEncoding { get; set; }
    }
}
