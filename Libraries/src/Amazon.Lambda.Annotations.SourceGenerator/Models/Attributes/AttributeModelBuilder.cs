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
                model = new AttributeModel<LambdaFunctionAttribute>
                {
                    Data = data,
                    Type = TypeModelBuilder.Build(att.AttributeClass, context)
                };
            }
            else if (att.AttributeClass.Equals(context.Compilation.GetTypeByMetadataName(TypeFullNames.FromQueryAttribute), SymbolEqualityComparer.Default))
            {
                var data = FromQueryAttributeBuilder.Build(att);
                model = new AttributeModel<FromQueryAttribute>
                {
                    Data = data,
                    Type = TypeModelBuilder.Build(att.AttributeClass, context)
                };
            }
            else if (att.AttributeClass.Equals(context.Compilation.GetTypeByMetadataName(TypeFullNames.FromHeaderAttribute), SymbolEqualityComparer.Default))
            {
                var data = FromHeaderAttributeBuilder.Build(att);
                model = new AttributeModel<FromHeaderAttribute>
                {
                    Data = data,
                    Type = TypeModelBuilder.Build(att.AttributeClass, context)
                };
            }
            else if (att.AttributeClass.Equals(context.Compilation.GetTypeByMetadataName(TypeFullNames.FromRouteAttribute), SymbolEqualityComparer.Default))
            {
                var data = FromRouteAttributeBuilder.Build(att);
                model = new AttributeModel<FromRouteAttribute>
                {
                    Data = data,
                    Type = TypeModelBuilder.Build(att.AttributeClass, context)
                };
            }
            else if (att.AttributeClass.Equals(context.Compilation.GetTypeByMetadataName(TypeFullNames.HttpApiAttribute), SymbolEqualityComparer.Default))
            {
                var data = HttpApiAttributeBuilder.Build(att);
                model = new AttributeModel<HttpApiAttribute>
                {
                    Data = data,
                    Type = TypeModelBuilder.Build(att.AttributeClass, context)
                };
            }
            else if (att.AttributeClass.Equals(context.Compilation.GetTypeByMetadataName(TypeFullNames.RestApiAttribute), SymbolEqualityComparer.Default))
            {
                var data = RestApiAttributeBuilder.Build(att);
                model = new AttributeModel<RestApiAttribute>
                {
                    Data = data,
                    Type = TypeModelBuilder.Build(att.AttributeClass, context)
                };
            }
            else
            {
                model = new AttributeModel
                {
                    Type = TypeModelBuilder.Build(att.AttributeClass, context)
                };
            }

            return model;
        }
    }
}