using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// <see cref="LambdaMethodModel"/> builder.
    /// </summary>
    public static class LambdaMethodModelBuilder
    {
        public static LambdaMethodModel Build(IMethodSymbol lambdaMethodSymbol,
            IMethodSymbol configureMethodSymbol,
            GeneratorExecutionContext context)
        {
            var model = new LambdaMethodModel
            {
                IsAsync = lambdaMethodSymbol.IsAsync,
                ReturnType = TypeModelBuilder.Build(lambdaMethodSymbol.ReturnType),
                ReturnsVoidOrTask = lambdaMethodSymbol.ReturnsVoid || lambdaMethodSymbol.ReturnType.Equals(context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"), SymbolEqualityComparer.Default),
                Parameters = ParameterModelBuilder.Build(lambdaMethodSymbol, context),
                Name = lambdaMethodSymbol.Name,
                ContainingNamespace = lambdaMethodSymbol.ContainingNamespace.ToDisplayString(),
                Events = EventTypeBuilder.Build(lambdaMethodSymbol, context),
                ContainingType = TypeModelBuilder.Build(lambdaMethodSymbol.ContainingType),
                UsingDependencyInjection = configureMethodSymbol != null
            };

            return model;
        }
    }
}