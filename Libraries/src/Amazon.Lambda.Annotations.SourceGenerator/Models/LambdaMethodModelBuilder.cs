using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
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
                ReturnType = TypeModelBuilder.Build(lambdaMethodSymbol.ReturnType, context),
                ReturnsVoidOrTask = lambdaMethodSymbol.ReturnsVoid || lambdaMethodSymbol.ReturnType.Equals(context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"), SymbolEqualityComparer.Default),
                Parameters = ParameterModelBuilder.Build(lambdaMethodSymbol, context),
                Name = lambdaMethodSymbol.Name,
                ContainingNamespace = lambdaMethodSymbol.ContainingNamespace.ToDisplayString(),
                Events = EventTypeBuilder.Build(lambdaMethodSymbol, context),
                ContainingType = TypeModelBuilder.Build(lambdaMethodSymbol.ContainingType, context),
                UsingDependencyInjection = configureMethodSymbol != null,
                Attributes = lambdaMethodSymbol.GetAttributes().Select(att => AttributeModelBuilder.Build(att, context)).ToList()
            };

            return model;
        }
    }
}