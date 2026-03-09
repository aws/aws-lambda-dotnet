namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// The payload format version for an API Gateway HTTP API Lambda authorizer.
    /// This maps to the <c>AuthorizerPayloadFormatVersion</c> property in the SAM template.
    /// </summary>
    public enum AuthorizerPayloadFormatVersion
    {
        /// <summary>
        /// Payload format version 1.0
        /// </summary>
        V1,

        /// <summary>
        /// Payload format version 2.0
        /// </summary>
        V2
    }
}