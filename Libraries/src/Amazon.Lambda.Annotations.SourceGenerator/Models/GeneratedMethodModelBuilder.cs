using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Annotations.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// <see cref="GeneratedMethodModel"/> builder.
    /// </summary>
    public static class GeneratedMethodModelBuilder
    {
        public static GeneratedMethodModel Build(IMethodSymbol lambdaMethodSymbol,
            IMethodSymbol configureMethodSymbol,
            GeneratorExecutionContext context)
        {
            var model = new GeneratedMethodModel
            {
                Usings = BuildUsings(lambdaMethodSymbol, configureMethodSymbol, context),
                RequestType = BuildRequestType(lambdaMethodSymbol, context),
                ResponseType = BuildResponseType(lambdaMethodSymbol, context),
                ContainingType = BuildContainingType(lambdaMethodSymbol),
            };
            return model;
        }

        private static IList<string> BuildUsings(IMethodSymbol lambdaMethodSymbol,
            IMethodSymbol configureMethodSymbol,
            GeneratorExecutionContext context)
        {
            var namespaces = new List<string>
            {
                "System",
                "System.Collections.Generic"
            };

            if (lambdaMethodSymbol.IsAsync)
            {
                namespaces.Add("System.Threading.Tasks");
            }

            if (configureMethodSymbol != null)
            {
                namespaces.Add("Microsoft.Extensions.DependencyInjection");
            }

            namespaces.Add("Amazon.Lambda.Core");

            if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.APIRouteAttribute))
            {
                namespaces.Add("Amazon.Lambda.APIGatewayEvents");
            }
            return namespaces;
        }

        private static TypeModel BuildResponseType(IMethodSymbol lambdaMethodSymbol,
            GeneratorExecutionContext context)
        {
            if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.APIRouteAttribute))
            {
                var symbol = context.Compilation.GetTypeByMetadataName(TypeFullNames.APIGatewayProxyResponse);
                return TypeModelBuilder.Build(symbol);
            }

            throw new NotImplementedException();
        }

        private static TypeModel BuildRequestType(IMethodSymbol lambdaMethodSymbol,
            GeneratorExecutionContext context)
        {
            if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.APIRouteAttribute))
            {
                var symbol = context.Compilation.GetTypeByMetadataName(TypeFullNames.APIGatewayProxyRequest);
                return TypeModelBuilder.Build(symbol);
            }

            throw new NotImplementedException();
        }

        private static TypeModel BuildContainingType(IMethodSymbol lambdaMethodSymbol)
        {
            var name = $"{lambdaMethodSymbol.ContainingType.Name}_{lambdaMethodSymbol.Name}_Generated";
            var fullName = $"{lambdaMethodSymbol.ContainingNamespace}.{name}";
            var model = new TypeModel
            {
                Name = name,
                FullName = fullName,
                IsValueType = false
            };

            return model;
        }
    }
}