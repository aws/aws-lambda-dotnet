namespace Amazon.Lambda.TestTool
{
    /// <summary>
    /// Defines the contract for translating an ASP.NET Core HTTP request to an API Gateway proxy request.
    /// </summary>
    public interface IApiGatewayRequestTranslator
    {
        /// <summary>
        /// Translates an ASP.NET Core HTTP request to an API Gateway proxy request.
        /// </summary>
        /// <param name="request">The ASP.NET Core HTTP request to translate.</param>
        /// <param name="pathParameters">The path parameters extracted from the request URL. For example, if the resource is "/users/{userId}/orders/{orderId}" and the actual path is "/users/123/orders/456", then pathParameters would be { "userId": "123", "orderId": "456" }.</param>
        /// <param name="resource">The API Gateway resource path. This is the template path defined in API Gateway, e.g., "/users/{userId}/orders/{orderId}".</param>
        /// <returns>An object representing the API Gateway proxy request (either APIGatewayProxyRequest or APIGatewayHttpApiV2ProxyRequest).</returns>
        /// <example>
        /// For a request to "https://api.example.com/users/123/orders/456?status=pending":
        /// - request: The HttpRequest object representing this request
        /// - pathParameters: { "userId": "123", "orderId": "456" }
        /// - resource: "/users/{userId}/orders/{orderId}"
        /// </example>
        object TranslateFromHttpRequest(HttpRequest request, IDictionary<string, string> pathParameters, string resource);
    }
}
