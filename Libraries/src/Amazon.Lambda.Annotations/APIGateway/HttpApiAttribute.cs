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
        /// Constructs a <see cref="HttpApiAttribute"/>
        /// </summary>
        /// <param name="method"></param>
        /// <param name="template"></param>
        public HttpApiAttribute(LambdaHttpMethod method, string template)
        {
            Template = template;
            Method = method;
        }
    }
}