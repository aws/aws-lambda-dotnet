using System;

namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// Configures the Lambda function to be called from an API Gateway REST API
    /// </summary>
    /// <remarks>
    /// The HTTP method and resource path are required to be set on the attribute.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method)]
    public class RestApiAttribute : Attribute
    {
        /// <summary>
        /// Resource path
        /// </summary>
        public string Template { get; set; }

        /// <inheritdoc cref="LambdaHttpMethod" />
        public LambdaHttpMethod Method { get; set; }

        /// <summary>
        /// Name of the REST API Lambda authorizer to protect this endpoint.
        /// Must match the Name property of a <see cref="RestApiAuthorizerAttribute"/> in this project.
        /// Leave null/empty for public (unauthenticated) endpoints.
        /// </summary>
        public string Authorizer { get; set; }

        /// <summary>
        /// Constructs a <see cref="RestApiAttribute"/>
        /// </summary>
        public RestApiAttribute(LambdaHttpMethod method, string template)
        {
            Template = template;
            Method = method;
        }
    }
}