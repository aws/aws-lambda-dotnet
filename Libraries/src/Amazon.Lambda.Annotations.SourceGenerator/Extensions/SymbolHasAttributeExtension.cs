using System.Linq;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Extensions
{
    public static class SymbolHasAttributeExtension
    {
        /// <summary>
        /// Returns true if symbol has an given attribute.
        /// </summary>
        /// <param name="symbol">Symbol exposed by the compiler.</param>
        /// <param name="context">Source generator context.</param>
        /// <param name="fullyQualifiedName">Fully qualified type name.</param>
        /// <returns></returns>
        public static bool HasAttribute(this ISymbol symbol, GeneratorExecutionContext context,
            string fullyQualifiedName)
        {
            return symbol.GetAttributes()
                .Any(att =>
                {
                    if (att.AttributeClass == null)
                    {
                        return false;
                    }

                    return att.AttributeClass.Equals(context.Compilation.GetTypeByMetadataName(fullyQualifiedName),
                        SymbolEqualityComparer.Default);
                });
        }

        /// <summary>
        /// Returns attribute data if symbol has an given attribute.
        /// If there symbol doesn't have the attribute, returns null.
        /// </summary>
        /// <param name="symbol">Symbol exposed by the compiler.</param>
        /// <param name="context">Source generator context.</param>
        /// <param name="fullyQualifiedName">Fully qualified type name.</param>
        /// <returns><see cref="AttributeData"/> for the given attribute.</returns>
        public static AttributeData GetAttributeData(this ISymbol symbol, GeneratorExecutionContext context,
            string fullyQualifiedName)
        {
            return symbol.GetAttributes()
                .FirstOrDefault(att =>
                {
                    if (att.AttributeClass == null)
                    {
                        return false;
                    }

                    return att.AttributeClass.Equals(context.Compilation.GetTypeByMetadataName(fullyQualifiedName),
                        SymbolEqualityComparer.Default);
                });
        }
    }
}