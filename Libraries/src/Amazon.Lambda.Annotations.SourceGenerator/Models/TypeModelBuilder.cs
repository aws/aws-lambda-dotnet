using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// <see cref="TypeModel"/> builder.
    /// </summary>
    public static class TypeModelBuilder
    {
        public static TypeModel Build(ITypeSymbol symbol, GeneratorExecutionContext context)
        {
            var model = new TypeModel
            {
                Name = symbol.Name,
                FullName = symbol.ToDisplayString(),
                IsValueType = symbol.IsValueType,
                IsGenericType = ((INamedTypeSymbol)symbol)?.IsGenericType ?? false,
                TypeArguments = ((INamedTypeSymbol)symbol)?.TypeArguments.Select(arg => Build(arg, context)).ToList(),
                IsEnumerable = ((INamedTypeSymbol)symbol)?.Interfaces.Any(it => it.Equals(context.Compilation.GetTypeByMetadataName(TypeFullNames.IEnumerable), SymbolEqualityComparer.Default)) ?? false
            };

            return model;
        }
    }
}