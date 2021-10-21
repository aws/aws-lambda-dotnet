using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// <see cref="LambdaFunctionModel"/> builder.
    /// </summary>
    public static class LambdaFunctionModelBuilder
    {
        public static LambdaFunctionModel Build(IMethodSymbol lambdaMethodSymbol, IMethodSymbol configureMethodSymbol, GeneratorExecutionContext context)
        {
            var generatedMethod = GeneratedMethodModelBuilder.Build(lambdaMethodSymbol, configureMethodSymbol, context);
            var lambdaMethod = LambdaMethodModelBuilder.Build(lambdaMethodSymbol, configureMethodSymbol, context);
            var model = new LambdaFunctionModel()
            {
                GeneratedMethod = generatedMethod,
                LambdaMethod = lambdaMethod,
                Serializer = "System.Text.Json.JsonSerializer", // TODO: replace serializer with assembly serializer
                StartupType = configureMethodSymbol != null ? TypeModelBuilder.Build(configureMethodSymbol.ContainingType) : null,
            };

            return model;
        }
    }
}