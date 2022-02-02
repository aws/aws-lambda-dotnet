using System.Collections.Generic;

namespace Amazon.Lambda.APIGatewayEvents
{
    /// <summary>
    /// For requests coming in to a custom API Gateway authorizer function.
    /// https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-lambda-authorizer.html
    /// </summary>
    public class APIGatewayCustomAuthorizerV2Request
    {
        /// <summary>
        /// Gets or sets the 'type' property.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the 'routeArn' property.
        /// </summary>
        public string RouteArn { get; set; }

        /// <summary>
        /// List of identity sources for the request.
        /// </summary>
        public List<string> IdentitySource { get; set; }

        /// <summary>
        /// Gets or sets the 'routeKey' property.
        /// </summary>
        public string RouteKey { get; set; }

        /// <summary>
        /// Raw url path for the caller.
        /// </summary>
        public string RawPath { get; set; }

        /// <summary>
        /// Raw query string for the caller.
        /// </summary>
        public string RawQueryString { get; set; }

        /// <summary>
        /// The cookies sent with the request.
        /// </summary>
        public List<string> Cookies { get; set; }

        /// <summary>
        /// The headers sent with the request.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }

        /// <summary>
        /// The query string parameters that were part of the request.
        /// </summary>
        public Dictionary<string, string> QueryStringParameters { get; set; }

        /// <summary>
        /// The path parameters that were part of the request.
        /// </summary>
        public Dictionary<string, string> PathParameters { get; set; }

        /// <summary>
        /// The stage variables defined for the stage in API Gateway.
        /// </summary>
        public Dictionary<string, string> StageVariables { get; set; }

        /// <summary>
        /// The request context for the request.
        /// </summary>
        public APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext RequestContext { get; set; }
    }
}
