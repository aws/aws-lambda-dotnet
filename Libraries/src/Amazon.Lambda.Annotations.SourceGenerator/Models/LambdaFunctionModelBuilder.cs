using System;
using System.Linq;
using Amazon.Lambda.Annotations.SourceGenerator.Diagnostics;
using Amazon.Lambda.Annotations.SourceGenerator.Extensions;
using Amazon.Lambda.Annotations.SourceGenerator.Validation;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// <see cref="LambdaFunctionModel"/> builder.
    /// </summary>
    public static class LambdaFunctionModelBuilder
    {
        public static LambdaFunctionModel BuildAndValidate(IMethodSymbol lambdaMethodSymbol, Location LambdamethodLocation, IMethodSymbol configureMethodSymbol, GeneratorExecutionContext context, bool isExecutable, string runtime, DiagnosticReporter diagnosticReporter)
        {
            // We need to check for the necessary dependencies before attempting to build the LambdaFunctionModel otherwise the generator blows up with unknown exceptions.
            if (!LambdaFunctionValidator.ValidateDependencies(context, lambdaMethodSymbol, LambdamethodLocation, diagnosticReporter))
            {
                return new LambdaFunctionModel() { IsValid = false };
            }

            var lambdaFunctionModel = Build(lambdaMethodSymbol, configureMethodSymbol, context, isExecutable, runtime);
            lambdaFunctionModel.IsValid = LambdaFunctionValidator.ValidateFunction(context, lambdaMethodSymbol, LambdamethodLocation, lambdaFunctionModel, diagnosticReporter);
            return lambdaFunctionModel;
        }

        private static LambdaFunctionModel Build(IMethodSymbol lambdaMethodSymbol, IMethodSymbol configureMethodSymbol, GeneratorExecutionContext context, bool isExecutable, string runtime)
        {
            var lambdaMethod = LambdaMethodModelBuilder.Build(lambdaMethodSymbol, configureMethodSymbol, context);
            var generatedMethod = GeneratedMethodModelBuilder.Build(lambdaMethodSymbol, configureMethodSymbol, lambdaMethod, context);
            var model = new LambdaFunctionModel()
            {
                GeneratedMethod = generatedMethod,
                LambdaMethod = lambdaMethod,
                SerializerInfo = GetSerializerInfoAttribute(context, lambdaMethodSymbol),
                StartupType = configureMethodSymbol != null ? TypeModelBuilder.Build(configureMethodSymbol.ContainingType, context) : null,
                SourceGeneratorVersion = context.Compilation
                    .ReferencedAssemblyNames.FirstOrDefault(x => string.Equals(x.Name, "Amazon.Lambda.Annotations"))
                    ?.Version.ToString(),
                IsExecutable = isExecutable,
                Runtime = runtime,
            };

            return model;
        }

        private static LambdaSerializerInfo GetSerializerInfoAttribute(GeneratorExecutionContext context, IMethodSymbol methodModel)
        {
            var serializerString = TypeFullNames.DefaultLambdaSerializer;

            ISymbol symbol = null;

            // First check if method has the Lambda Serializer.
            if (methodModel.HasAttribute(
                    context,
                    TypeFullNames.LambdaSerializerAttribute))
            {
                symbol = methodModel;
            }
            // Then check assembly
            else if (methodModel.ContainingAssembly.HasAttribute(
                    context,
                    TypeFullNames.LambdaSerializerAttribute))
            {
                symbol = methodModel.ContainingAssembly;
            }
            // Else return the default serializer.
            else
            {
                return new LambdaSerializerInfo(serializerString);
            }

            var attribute = symbol.GetAttributes().FirstOrDefault(attr => attr.AttributeClass.Name == TypeFullNames.LambdaSerializerAttributeWithoutNamespace);

            var serializerValue = attribute.ConstructorArguments.FirstOrDefault(kvp => kvp.Type.Name == nameof(Type)).Value;

            if (serializerValue != null)
            {
                serializerString = serializerValue.ToString();
            }

            return new LambdaSerializerInfo(serializerString);
        }
    }
}
