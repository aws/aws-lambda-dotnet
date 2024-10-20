namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// The <see href="https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-develop-integrations-lambda.html#http-api-develop-integrations-lambda.proxy-format">
    /// Payload Format Version</see> for an API Gateway HTTP API.
    /// </summary>
    public enum HttpApiVersion
    {
        /// <summary>
        /// Version 1 of Http Api
        /// </summary>
        V1,
        /// <summary>
        /// Version 2 of Http Api
        /// </summary>
        V2
    }
}