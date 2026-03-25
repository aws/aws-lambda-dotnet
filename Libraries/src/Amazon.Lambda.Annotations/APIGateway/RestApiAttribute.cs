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
        /// The .NET method name of the REST API Lambda authorizer to protect this endpoint.
        /// Must match the method name that has the <see cref="RestApiAuthorizerAttribute"/> applied to it.
        /// Use the <c>nameof</c> operator for compile-time safety (e.g. <c>Authorizer = nameof(MyAuthorizerMethod)</c>).
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