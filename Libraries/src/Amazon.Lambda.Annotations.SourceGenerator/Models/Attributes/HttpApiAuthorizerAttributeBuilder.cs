using System.Linq;
using Amazon.Lambda.Annotations.APIGateway;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    /// <summary>
    /// Builder for <see cref="HttpApiAuthorizerAttribute"/>.
    /// </summary>
    public static class HttpApiAuthorizerAttributeBuilder
    {
        /// <summary>
        /// Builds an <see cref="HttpApiAuthorizerAttribute"/> from the Roslyn attribute data.
        /// </summary>
        /// <param name="att">The attribute data from Roslyn</param>
        /// <returns>The populated attribute instance</returns>
        public static HttpApiAuthorizerAttribute Build(AttributeData att)
        {
            var attribute = new HttpApiAuthorizerAttribute();

            foreach (var namedArg in att.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case nameof(HttpApiAuthorizerAttribute.IdentityHeader):
                        attribute.IdentityHeader = namedArg.Value.Value as string ?? "Authorization";
                        break;
                    case nameof(HttpApiAuthorizerAttribute.EnableSimpleResponses):
                        attribute.EnableSimpleResponses = namedArg.Value.Value is bool val ? val : true;
                        break;
                    case nameof(HttpApiAuthorizerAttribute.AuthorizerPayloadFormatVersion):
                        attribute.AuthorizerPayloadFormatVersion = namedArg.Value.Value is int enumVal
                            ? (AuthorizerPayloadFormatVersion)enumVal
                            : AuthorizerPayloadFormatVersion.V2;
                        break;
                    case nameof(HttpApiAuthorizerAttribute.ResultTtlInSeconds):
                        attribute.ResultTtlInSeconds = namedArg.Value.Value is int ttl ? ttl : 0;
                        break;
                }
            }

            return attribute;
        }

        /// <summary>
        /// Builds an <see cref="AuthorizerModel"/> from the attribute and lambda function resource name.
        /// </summary>
        /// <param name="att">The attribute data from Roslyn</param>
        /// <param name="lambdaResourceName">The CloudFormation resource name for the Lambda function</param>
        /// <returns>The populated authorizer model</returns>
        public static AuthorizerModel BuildModel(AttributeData att, string lambdaResourceName)
        {
            var attribute = Build(att);
            return BuildModel(attribute, lambdaResourceName);
        }

        /// <summary>
        /// Builds an <see cref="AuthorizerModel"/> from the attribute and lambda function resource name.
        /// </summary>
        /// <param name="attribute">The parsed attribute</param>
        /// <param name="lambdaResourceName">The CloudFormation resource name for the Lambda function</param>
        /// <returns>The populated authorizer model</returns>
        public static AuthorizerModel BuildModel(HttpApiAuthorizerAttribute attribute, string lambdaResourceName)
        {
            return new AuthorizerModel
            {
                LambdaResourceName = lambdaResourceName,
                AuthorizerType = AuthorizerType.HttpApi,
                IdentityHeader = attribute.IdentityHeader,
                ResultTtlInSeconds = attribute.ResultTtlInSeconds,
                EnableSimpleResponses = attribute.EnableSimpleResponses,
                AuthorizerPayloadFormatVersion = attribute.AuthorizerPayloadFormatVersion
            };
        }
    }
}