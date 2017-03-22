namespace Amazon.Lambda.AspNetCoreServer
{
    /// <summary>
    /// Indicates how response content from a controller action should be treated,
    /// possibly requiring a transformation to comply with the expected binary disposition.
    /// </summary>
    public enum ResponseContentEncoding
    {
        /// Indicates the response content should already be UTF-8-friendly and should be
        /// returned without any further transformation or encoding.  This typically
        /// indicates a text response.
        Default = 0,

        /// Indicates the response content should be Base64 encoded before being returned.
        /// This is typically used to indicate a binary response.
        Base64,
    }
}