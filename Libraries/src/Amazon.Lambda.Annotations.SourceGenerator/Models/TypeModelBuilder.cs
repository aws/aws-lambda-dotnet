using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// <see cref="TypeModel"/> builder.
    /// </summary>
    public static class TypeModelBuilder
    {
        public static TypeModel Build(ITypeSymbol symbol)
        {
            var model = new TypeModel
            {
                Name = symbol.Name,
                FullName = symbol.ToDisplayString(),
                IsValueType = symbol.IsValueType
            };

            return model;
        }
    }
}