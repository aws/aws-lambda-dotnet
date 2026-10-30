using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// <see cref="ParameterModel"/> builder.
    /// </summary>
    public static class ParameterModelBuilder
    {
        public static IList<ParameterModel> Build(IMethodSymbol methodSymbol,
            GeneratorExecutionContext context)
        {
            var models = new List<ParameterModel>();

            foreach (var parameter in methodSymbol.Parameters)
            {
                var type = TypeModelBuilder.Build(parameter.Type, context);
                var isFromServicesParameter = parameter.GetAttributes()
                    .Any(attribute => attribute.AttributeClass?.ToDisplayString() == TypeFullNames.FromServiceAttribute);
                var parameterTypeName = parameter.Type.ToDisplayString();
                var isGeneratedRequestType = TypeFullNames.ApiGatewayRequests.Contains(parameterTypeName)
                    || TypeFullNames.ALBRequests.Contains(parameterTypeName)
                    || TypeFullNames.AuthorizerRequests.Contains(parameterTypeName);

                // Preserve the type-name comparisons used by request-specific generator templates.
                if (isFromServicesParameter
                    && parameterTypeName != TypeFullNames.ILambdaContext
                    && !isGeneratedRequestType
                    && RequiresGlobalQualifier(methodSymbol.ContainingNamespace, parameter.Type.ContainingNamespace))
                {
                    type.FullName = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                }

                models.Add(new ParameterModel
                {
                    Name = parameter.Name,
                    Type = type,
                    Attributes = parameter.GetAttributes().Select(att => AttributeModelBuilder.Build(att, context)).ToList()
                });
            }

            return models;
        }

        private static bool RequiresGlobalQualifier(INamespaceSymbol containingNamespace, INamespaceSymbol typeNamespace)
        {
            if (typeNamespace == null || typeNamespace.IsGlobalNamespace)
                return false;

            while (!typeNamespace.ContainingNamespace.IsGlobalNamespace)
                typeNamespace = typeNamespace.ContainingNamespace;

            var rootNamespaceName = typeNamespace.Name;
            for (var scope = containingNamespace; !scope.IsGlobalNamespace; scope = scope.ContainingNamespace)
            {
                if (scope.GetMembers(rootNamespaceName).Any())
                    return true;
            }

            return false;
        }
    }
}
