using System.Collections.Generic;

namespace Amazon.Lambda.APIGatewayEvents
{
    /// <summary>
    /// For requests coming in to a custom API Gateway authorizer function.
    /// </summary>
    public class APIGatewayCustomAuthorizerRequest
    {
        /// <summary>
        /// Gets or sets the 'type' property.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the 'authorizationToken' property.
        /// </summary>
        public string AuthorizationToken { get; set; }

        /// <summary>
        /// Gets or sets the 'methodArn' property.
        /// </summary>
        public string MethodArn { get; set; }

        /// <summary>
        /// The url path for the caller. For Request type API Gateway Custom Authorizer only.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The HTTP method used. For Request type API Gateway Custom Authorizer only.
        /// </summary>
        public string HttpMethod { get; set; }

        /// <summary>
        /// The headers sent with the request. For Request type API Gateway Custom Authorizer only.
        /// </summary>
        public IDictionary<string, string> Headers {get;set;}

        /// <summary>
        /// The query string parameters that were part of the request. For Request type API Gateway Custom Authorizer only.
        /// </summary>
        public IDictionary<string, string> QueryStringParameters { get; set; }

        /// <summary>
        /// The path parameters that were part of the request. For Request type API Gateway Custom Authorizer only.
        /// </summary>
        public IDictionary<string, string> PathParameters { get; set; }

        /// <summary>
        /// The stage variables defined for the stage in API Gateway. For Request type API Gateway Custom Authorizer only.
        /// </summary>
        public IDictionary<string, string> StageVariables { get; set; }

        /// <summary>
        /// The request context for the request. For Request type API Gateway Custom Authorizer only.
        /// </summary>
        public APIGatewayProxyRequest.ProxyRequestContext RequestContext { get; set; }

    }
}
