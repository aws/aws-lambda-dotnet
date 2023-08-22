using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Annotations.APIGateway;
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
            LambdaMethodModel lambdaMethodModel,
            GeneratorExecutionContext context)
        {
            var model = new GeneratedMethodModel
            {
                Usings = BuildUsings(lambdaMethodModel, lambdaMethodSymbol, configureMethodSymbol, context),
                Parameters = BuildParameters(lambdaMethodSymbol, lambdaMethodModel, context),
                ReturnType = BuildResponseType(lambdaMethodSymbol, lambdaMethodModel, context),
                ContainingType = BuildContainingType(lambdaMethodSymbol),
            };
            return model;
        }

        private static IList<string> BuildUsings(LambdaMethodModel lambdaMethodModel, 
            IMethodSymbol lambdaMethodSymbol,
            IMethodSymbol configureMethodSymbol,
            GeneratorExecutionContext context)
        {
            var namespaces = new List<string>
            {
                "System",
                "System.Linq",
                "System.Collections.Generic",
                "System.Text",
                "System.Threading.Tasks",
                "System.IO"
            };

            if (configureMethodSymbol != null)
            {
                namespaces.Add("Microsoft.Extensions.DependencyInjection");
            }

            namespaces.Add("Amazon.Lambda.Core");

            if(lambdaMethodModel.ReturnsIHttpResults)
            {
                namespaces.Add("Amazon.Lambda.Annotations.APIGateway");
            }

            return namespaces;
        }

        private static TypeModel BuildResponseType(IMethodSymbol lambdaMethodSymbol,
            LambdaMethodModel lambdaMethodModel, GeneratorExecutionContext context)
        {
            var task = context.Compilation.GetTypeByMetadataName(TypeFullNames.Task1);

            if (lambdaMethodModel.ReturnsIHttpResults)
            {
                var typeStream = context.Compilation.GetTypeByMetadataName(TypeFullNames.Stream);
                if (lambdaMethodModel.ReturnsGenericTask)
                {
                    var genericTask = task.Construct(typeStream);
                    return TypeModelBuilder.Build(genericTask, context);
                }
                return TypeModelBuilder.Build(typeStream, context);
            }

            
            if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.RestApiAttribute))
            {
                var symbol = lambdaMethodModel.ReturnsVoidOrGenericTask ?
                    task.Construct(context.Compilation.GetTypeByMetadataName(TypeFullNames.APIGatewayProxyResponse)):
                    context.Compilation.GetTypeByMetadataName(TypeFullNames.APIGatewayProxyResponse);
                return TypeModelBuilder.Build(symbol, context);
            }
            else if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.HttpApiAttribute))
            {
                var version = GetHttpApiVersion(lambdaMethodSymbol, context);
                switch (version)
                {
                    case HttpApiVersion.V1:
                    {
                        var symbol = lambdaMethodModel.ReturnsVoidOrGenericTask ?
                            task.Construct(context.Compilation.GetTypeByMetadataName(TypeFullNames.APIGatewayProxyResponse)):
                            context.Compilation.GetTypeByMetadataName(TypeFullNames.APIGatewayProxyResponse);
                        return TypeModelBuilder.Build(symbol, context);
                    }
                    case HttpApiVersion.V2:
                    {
                        var symbol = lambdaMethodModel.ReturnsVoidOrGenericTask ?
                            task.Construct(context.Compilation.GetTypeByMetadataName(TypeFullNames.APIGatewayHttpApiV2ProxyResponse)):
                            context.Compilation.GetTypeByMetadataName(TypeFullNames.APIGatewayHttpApiV2ProxyResponse);
                        return TypeModelBuilder.Build(symbol, context);
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else
            {
                return lambdaMethodModel.ReturnType;
            }
        }

        private static HttpApiVersion GetHttpApiVersion(IMethodSymbol lambdaMethodSymbol, GeneratorExecutionContext context)
        {
            var httpApiAttribute = lambdaMethodSymbol.GetAttributeData(context, TypeFullNames.HttpApiAttribute);
            if (httpApiAttribute.ConstructorArguments.IsDefaultOrEmpty)
            {
                throw new InvalidOperationException($"{TypeFullNames.HttpApiAttribute} must have a constructor with parameter.");
            }

            var versionArgument = httpApiAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "Version").Value;
            if (versionArgument.Type == null)
            {
                return HttpApiVersion.V2;
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

        private static IList<ParameterModel> BuildParameters(IMethodSymbol lambdaMethodSymbol,
            LambdaMethodModel lambdaMethodModel, GeneratorExecutionContext context)
        {
            var parameters = new List<ParameterModel>();

            var contextParameter = new ParameterModel
            {
                Name = "__context__",
                Type = new TypeModel
                {
                    FullName = TypeFullNames.ILambdaContext
                }
            };

            if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.RestApiAttribute))
            {
                var symbol = context.Compilation.GetTypeByMetadataName(TypeFullNames.APIGatewayProxyRequest);
                var type = TypeModelBuilder.Build(symbol, context);
                var requestParameter = new ParameterModel
                {
                    Name = "__request__",
                    Type = type
                };
                parameters.Add(requestParameter);
                parameters.Add(contextParameter);
            }
            else if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.HttpApiAttribute))
            {
                var version = GetHttpApiVersion(lambdaMethodSymbol, context);
                TypeModel type;
                switch (version)
                {
                    case HttpApiVersion.V1:
                    {
                        var symbol = context.Compilation.GetTypeByMetadataName(TypeFullNames.APIGatewayProxyRequest);
                        type = TypeModelBuilder.Build(symbol, context);
                        break;
                    }
                    case HttpApiVersion.V2:
                    {
                        var symbol = context.Compilation.GetTypeByMetadataName(TypeFullNames.APIGatewayHttpApiV2ProxyRequest);
                        type = TypeModelBuilder.Build(symbol, context);
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                var requestParameter = new ParameterModel
                {
                    Name = "__request__",
                    Type = type
                };
                parameters.Add(requestParameter);
                parameters.Add(contextParameter);
            }
            else
            {
                // Lambda method with no event attribute are plain lambda functions, therefore, generated method will have
                // same parameter as original method except DI injected parameters
                foreach (var param in lambdaMethodModel.Parameters)
                {
                    if (param.Attributes.Any(att => att.Type.FullName == TypeFullNames.FromServiceAttribute))
                    {
                        continue;
                    }
                    // If the Lambda function is taking in the ILambdaContext object make sure in the generated wrapper code we
                    // use the same system name for the ILambdaContext variable as all of the other places use.
                    else if(param.Type.FullName == TypeFullNames.ILambdaContext)
                    {
                        param.Name = "__context__";
                    }

                    parameters.Add(param);
                }
            }

            return parameters;
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