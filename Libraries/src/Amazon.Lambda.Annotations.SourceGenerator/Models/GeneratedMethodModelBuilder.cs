using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Annotations;
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

            if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.RestApiAttribute) || lambdaMethodSymbol.HasAttribute(context, TypeFullNames.HttpApiAttribute))
            {
                namespaces.Add("Amazon.Lambda.APIGatewayEvents");
            }
            return namespaces;
        }

        private static TypeModel BuildResponseType(IMethodSymbol lambdaMethodSymbol, GeneratorExecutionContext context)
        {
            if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.RestApiAttribute))
            {
                var symbol = context.Compilation.GetTypeByMetadataName(TypeFullNames.APIGatewayProxyResponse);
                return TypeModelBuilder.Build(symbol);
            }
            else if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.HttpApiAttribute))
            {
                var version = GetHttpApiVersion(lambdaMethodSymbol, context);
                switch (version)
                {
                    case HttpApiVersion.V1:
                    {
                        var symbol = context.Compilation.GetTypeByMetadataName(TypeFullNames.APIGatewayProxyResponse);
                        return TypeModelBuilder.Build(symbol);;
                    }
                    case HttpApiVersion.V2:
                    {
                        var symbol = context.Compilation.GetTypeByMetadataName(TypeFullNames.APIGatewayHttpApiV2ProxyResponse);
                        return TypeModelBuilder.Build(symbol);;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else
            {
                var symbol = context.Compilation.GetTypeByMetadataName(TypeFullNames.MemoryStream);
                return TypeModelBuilder.Build(symbol);
            }
        }

        private static HttpApiVersion GetHttpApiVersion(IMethodSymbol lambdaMethodSymbol, GeneratorExecutionContext context)
        {
            var httpApiAttribute = lambdaMethodSymbol.GetAttributeData(context, TypeFullNames.HttpApiAttribute);
            if (httpApiAttribute.ConstructorArguments.IsDefaultOrEmpty)
            {
                throw new InvalidOperationException($"{TypeFullNames.HttpApiAttribute} must have a constructor with parameter.");
            }

            var versionArgument = httpApiAttribute.ConstructorArguments[0];
            if (versionArgument.Type == null)
            {
                throw new InvalidOperationException($"{versionArgument.Type} type cannot be null for {TypeFullNames.HttpApiAttribute}.");
            }

            if (!versionArgument.Type.Equals(context.Compilation.GetTypeByMetadataName(TypeFullNames.HttpApiVersion), SymbolEqualityComparer.Default))
            {
                throw new InvalidOperationException($"Constructor parameter must be of type {TypeFullNames.HttpApiVersion}.");
            }

            if (versionArgument.Value == null)
            {
                throw new InvalidOperationException($"{versionArgument.Type} value cannot be null for {TypeFullNames.HttpApiAttribute}.");
            }

            return (HttpApiVersion)versionArgument.Value;
        }

        private static TypeModel BuildRequestType(IMethodSymbol lambdaMethodSymbol,
            GeneratorExecutionContext context)
        {
            if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.RestApiAttribute))
            {
                var symbol = context.Compilation.GetTypeByMetadataName(TypeFullNames.APIGatewayProxyRequest);
                return TypeModelBuilder.Build(symbol);
            }
            else if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.HttpApiAttribute))
            {
                var version = GetHttpApiVersion(lambdaMethodSymbol, context);
                switch (version)
                {
                    case HttpApiVersion.V1:
                    {
                        var symbol = context.Compilation.GetTypeByMetadataName(TypeFullNames.APIGatewayProxyRequest);
                        return TypeModelBuilder.Build(symbol);;
                    }
                    case HttpApiVersion.V2:
                    {
                        var symbol = context.Compilation.GetTypeByMetadataName(TypeFullNames.APIGatewayHttpApiV2ProxyRequest);
                        return TypeModelBuilder.Build(symbol);;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else
            {
                var symbol = context.Compilation.GetTypeByMetadataName(TypeFullNames.MemoryStream);
                return TypeModelBuilder.Build(symbol);
            }
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