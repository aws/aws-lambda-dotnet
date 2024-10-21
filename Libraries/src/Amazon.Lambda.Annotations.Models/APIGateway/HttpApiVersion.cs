namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// The <see href="https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-develop-integrations-lambda.html#http-api-develop-integrations-lambda.proxy-format">
    /// Payload Format Version</see> for an API Gateway HTTP API.
    /// </summary>
    public enum HttpApiVersion
    {
        /// <summary>
        /// API Gateway HTTP API V1
        /// </summary>
        V1,
        /// <summary>
        /// API Gateway HTTP API V2
        /// </summary>
        V2
    }
}