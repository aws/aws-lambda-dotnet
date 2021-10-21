using System;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    /// <summary>
    /// <see cref="AttributeModel"/> builder.
    /// </summary>
    public static class AttributeModelBuilder
    {
        public static AttributeModel Build(AttributeData att, GeneratorExecutionContext context)
        {
            if (att.AttributeClass == null)
            {
                throw new NotSupportedException($"An attribute must have an attribute class. Attribute class is not found for {att}");
            }

            AttributeModel model;
            if (att.AttributeClass.Equals(context.Compilation.GetTypeByMetadataName(TypeFullNames.LambdaFunctionAttribute), SymbolEqualityComparer.Default))
            {
                var data = LambdaFunctionAttributeDataBuilder.Build(att);
                model = new AttributeModel<LambdaFunctionAttributeData>
                {
                    Data = data,
                    Type = TypeModelBuilder.Build(att.AttributeClass)
                };
            }
            else
            {
                model = new AttributeModel
                {
                    Type = TypeModelBuilder.Build(att.AttributeClass)
                };
            }

            return model;
        }
    }
}