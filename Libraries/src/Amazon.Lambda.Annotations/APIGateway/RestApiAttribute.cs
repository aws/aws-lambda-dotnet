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
        /// Constructs a <see cref="RestApiAttribute"/>
        /// </summary>
        /// <param name="method"></param>
        /// <param name="template"></param>
        public RestApiAttribute(LambdaHttpMethod method, string template)
        {
            Template = template;
            Method = method;
        }
    }
}