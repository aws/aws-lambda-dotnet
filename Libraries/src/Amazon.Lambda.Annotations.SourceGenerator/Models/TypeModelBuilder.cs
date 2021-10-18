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

        public static TypeModel Build(IParameterSymbol symbol)
        {
            var model = new TypeModel
            {
                Name = symbol.Name,
                FullName = symbol.Type.ToDisplayString(),
                IsValueType = symbol.Type.IsValueType
            };

            return model;
        }
    }
}