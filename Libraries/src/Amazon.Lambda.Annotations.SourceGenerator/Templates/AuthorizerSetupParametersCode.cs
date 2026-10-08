using System.Linq;
using System.Text;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;

namespace Amazon.Lambda.Annotations.SourceGenerator.Templates
{
    public partial class AuthorizerSetupParameters
    {
        private readonly LambdaFunctionModel _model;

        public string ParameterSignature { get; set; }

        public AuthorizerSetupParameters(LambdaFunctionModel model)
        {
            _model = model;
        }

        /// <summary>
        /// Returns true if any parameter is bound from a client supplied value (header, query string or route)
        /// and therefore requires conversion that may fail.
        /// </summary>
        private bool HasBoundParameters()
        {
            return _model.LambdaMethod.Parameters.Any(p =>
                p.Type.FullName != TypeFullNames.ILambdaContext &&
                !TypeFullNames.AuthorizerRequests.Contains(p.Type.FullName) &&
                !p.Attributes.Any(att => att.Type.FullName == TypeFullNames.FromServiceAttribute) &&
                p.Attributes.Any(att =>
                    att.Type.FullName == TypeFullNames.FromHeaderAttribute ||
                    att.Type.FullName == TypeFullNames.FromQueryAttribute ||
                    att.Type.FullName == TypeFullNames.FromRouteAttribute));
        }

        /// <summary>
        /// Generates the statements that deny the request when a client supplied value fails to convert to the
        /// parameter type. The user's authorizer method is not invoked so it never evaluates default values
        /// in place of the client supplied value.
        /// </summary>
        private string GenerateBindingFailureResponse()
        {
            const string indent = "                ";

            var httpApiAuthorizerAttribute = _model.LambdaMethod.Attributes.FirstOrDefault(att => att.Type.FullName == TypeFullNames.HttpApiAuthorizerAttribute) as AttributeModel<Amazon.Lambda.Annotations.APIGateway.HttpApiAuthorizerAttribute>;
            var isV2Request = httpApiAuthorizerAttribute != null && httpApiAuthorizerAttribute.Data.AuthorizerPayloadFormatVersion == Amazon.Lambda.Annotations.APIGateway.AuthorizerPayloadFormatVersion.V2;
            var methodArnExpression = isV2Request ? "__request__.RouteArn" : "__request__.MethodArn";

            var sb = new StringBuilder();
            if (_model.LambdaMethod.ReturnsIAuthorizerResult)
            {
                string format;
                if (httpApiAuthorizerAttribute != null && httpApiAuthorizerAttribute.Data.EnableSimpleResponses)
                {
                    format = "AuthorizerResultSerializationOptions.AuthorizerFormat.HttpApiSimple";
                }
                else if (httpApiAuthorizerAttribute != null)
                {
                    format = "AuthorizerResultSerializationOptions.AuthorizerFormat.HttpApiIamPolicy";
                }
                else
                {
                    format = "AuthorizerResultSerializationOptions.AuthorizerFormat.RestApi";
                }

                AppendLine(sb, $"{indent}return AuthorizerResults.Deny().Serialize(new AuthorizerResultSerializationOptions");
                AppendLine(sb, $"{indent}{{");
                AppendLine(sb, $"{indent}    Format = {format},");
                AppendLine(sb, $"{indent}    MethodArn = {methodArnExpression}");
                AppendLine(sb, $"{indent}}});");
                return sb.ToString();
            }

            var returnType = _model.LambdaMethod.ReturnsGenericTask && _model.LambdaMethod.ReturnType.TypeArguments.Count == 1
                ? _model.LambdaMethod.ReturnType.TypeArguments[0].FullName
                : _model.LambdaMethod.ReturnType.FullName;

            if (returnType == TypeFullNames.APIGatewayCustomAuthorizerV2SimpleResponse)
            {
                AppendLine(sb, $"{indent}return new {TypeFullNames.APIGatewayCustomAuthorizerV2SimpleResponse} {{ IsAuthorized = false }};");
            }
            else if (returnType == TypeFullNames.APIGatewayCustomAuthorizerResponse || returnType == TypeFullNames.APIGatewayCustomAuthorizerV2IamResponse)
            {
                AppendLine(sb, $"{indent}return new {returnType}");
                AppendLine(sb, $"{indent}{{");
                AppendLine(sb, $"{indent}    PrincipalID = \"user\",");
                AppendLine(sb, $"{indent}    PolicyDocument = new {TypeFullNames.APIGatewayCustomAuthorizerPolicy}");
                AppendLine(sb, $"{indent}    {{");
                AppendLine(sb, $"{indent}        Statement = new List<{TypeFullNames.APIGatewayCustomAuthorizerPolicy}.IAMPolicyStatement>");
                AppendLine(sb, $"{indent}        {{");
                AppendLine(sb, $"{indent}            new {TypeFullNames.APIGatewayCustomAuthorizerPolicy}.IAMPolicyStatement");
                AppendLine(sb, $"{indent}            {{");
                AppendLine(sb, $"{indent}                Effect = \"Deny\",");
                AppendLine(sb, $"{indent}                Action = new HashSet<string> {{ \"execute-api:Invoke\" }},");
                AppendLine(sb, $"{indent}                Resource = new HashSet<string> {{ {methodArnExpression} ?? \"*\" }}");
                AppendLine(sb, $"{indent}            }}");
                AppendLine(sb, $"{indent}        }}");
                AppendLine(sb, $"{indent}    }}");
                AppendLine(sb, $"{indent}}};");
            }
            else
            {
                // Unknown response shape. API Gateway maps an "Unauthorized" error from the authorizer to a 401 response.
                AppendLine(sb, $"{indent}throw new Exception(\"Unauthorized\");");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Appends a line using the same line ending the T4 templates emit so the generated code is consistent.
        /// </summary>
        private static void AppendLine(StringBuilder sb, string line)
        {
            sb.Append(line).Append("\r\n");
        }
    }
}
