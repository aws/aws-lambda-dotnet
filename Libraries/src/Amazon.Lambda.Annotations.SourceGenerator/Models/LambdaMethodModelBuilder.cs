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
                ReturnType = TypeModelBuilder.Build(lambdaMethodSymbol.ReturnType, context),
                ReturnsVoid = lambdaMethodSymbol.ReturnsVoid,
                ReturnsVoidTask = lambdaMethodSymbol.ReturnType.Equals(context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"), SymbolEqualityComparer.Default),
                ReturnsGenericTask = (lambdaMethodSymbol.ReturnType.BaseType?.Equals(context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"), SymbolEqualityComparer.Default)).GetValueOrDefault(),
                Parameters = ParameterModelBuilder.Build(lambdaMethodSymbol, context),
                Name = lambdaMethodSymbol.Name,
                ContainingAssembly = lambdaMethodSymbol.ContainingAssembly.Name,
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