using System.Linq;
using Amazon.Lambda.Annotations.APIGateway;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    /// <summary>
    /// Builder for <see cref="RestApiAuthorizerAttribute"/>.
    /// </summary>
    public static class RestApiAuthorizerAttributeBuilder
    {
        /// <summary>
        /// Builds a <see cref="RestApiAuthorizerAttribute"/> from the Roslyn attribute data.
        /// </summary>
        /// <param name="att">The attribute data from Roslyn</param>
        /// <returns>The populated attribute instance</returns>
        public static RestApiAuthorizerAttribute Build(AttributeData att)
        {
            var attribute = new RestApiAuthorizerAttribute();

            foreach (var namedArg in att.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case nameof(RestApiAuthorizerAttribute.Name):
                        attribute.Name = namedArg.Value.Value as string;
                        break;
                    case nameof(RestApiAuthorizerAttribute.IdentityHeader):
                        attribute.IdentityHeader = namedArg.Value.Value as string ?? "Authorization";
                        break;
                    case nameof(RestApiAuthorizerAttribute.Type):
                        attribute.Type = namedArg.Value.Value is int typeVal 
                            ? (RestApiAuthorizerType)typeVal 
                            : RestApiAuthorizerType.Token;
                        break;
                    case nameof(RestApiAuthorizerAttribute.ResultTtlInSeconds):
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
        public static AuthorizerModel BuildModel(RestApiAuthorizerAttribute attribute, string lambdaResourceName)
        {
            return new AuthorizerModel
            {
                Name = attribute.Name,
                LambdaResourceName = lambdaResourceName,
                AuthorizerType = AuthorizerType.RestApi,
                IdentityHeader = attribute.IdentityHeader,
                ResultTtlInSeconds = attribute.ResultTtlInSeconds,
                RestApiAuthorizerType = attribute.Type
            };
        }
    }
}