using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// <see cref="TypeModel"/> builder.
    /// </summary>
    internal static class TypeModelBuilder
    {
        public static TypeModel Build(ITypeSymbol symbol, GeneratorExecutionContext context)
        {
            bool isGenericType;
            if (symbol is INamedTypeSymbol)
                isGenericType = ((INamedTypeSymbol)symbol)?.IsGenericType ?? false;
            else
                isGenericType = false;

            IList<TypeModel> typeArguments;
            if (symbol is INamedTypeSymbol)
                typeArguments = ((INamedTypeSymbol)symbol)?.TypeArguments.Select(arg => Build(arg, context)).ToList();
            else
                typeArguments = new List<TypeModel>();

            bool isEnumerable;
            if (symbol is INamedTypeSymbol)
                isEnumerable = ((INamedTypeSymbol)symbol)?.Interfaces.Any(it => it.Equals(context.Compilation.GetTypeByMetadataName(TypeFullNames.IEnumerable), SymbolEqualityComparer.Default)) ?? false;
            else if (symbol is IDynamicTypeSymbol)
                isEnumerable = ((IDynamicTypeSymbol)symbol)?.Interfaces.Any(it => it.Equals(context.Compilation.GetTypeByMetadataName(TypeFullNames.IEnumerable), SymbolEqualityComparer.Default)) ?? false;
            else
                isEnumerable = false;

            var model = new TypeModel
            {
                Name = symbol.Name,
                FullName = symbol.ToDisplayString(),
                IsValueType = symbol.IsValueType,
                IsGenericType = isGenericType,
                TypeArguments = typeArguments,
                IsEnumerable = isEnumerable,
                HasNullableAnnotations = symbol.NullableAnnotation == NullableAnnotation.Annotated
            };

            return model;
        }
    }
}