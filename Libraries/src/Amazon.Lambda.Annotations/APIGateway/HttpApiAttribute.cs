using System;

namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// Configures the Lambda function to be called from an API Gateway HTTP API
    /// </summary>
    /// <remarks>
    /// The HTTP method, HTTP API payload version and resource path are required to be set on the attribute.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method)]
    public class HttpApiAttribute : Attribute
    {
        /// <inheritdoc cref="HttpApiVersion"/>
        public HttpApiVersion Version { get; set; } = HttpApiVersion.V2;

        /// <summary>
        /// Resource path
        /// </summary>
        public string Template { get; set; }

        /// <inheritdoc cref="LambdaHttpMethod"/>
        public LambdaHttpMethod Method { get; set; }

        /// <summary>
        /// Name of the HTTP API Lambda authorizer to protect this endpoint.
        /// Must match the Name property of an <see cref="HttpApiAuthorizerAttribute"/> in this project.
        /// Leave null/empty for public (unauthenticated) endpoints.
        /// </summary>
        public string Authorizer { get; set; }

        /// <summary>
        /// Constructs a <see cref="HttpApiAttribute"/>
        /// </summary>
        public HttpApiAttribute(LambdaHttpMethod method, string template)
        {
            Template = template;
            Method = method;
        }
    }
}