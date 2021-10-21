using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
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
            if (att.AttributeClass.Equals(context.Compilation.GetTypeByMetadataName(TypeFullNames.FromPathAttribute), SymbolEqualityComparer.Default))
            {
                var data = new FromPathAttributeData();
                foreach (var pair in att.NamedArguments)
                {
                    if (pair.Key == nameof(data.Name) && pair.Value.Value is string value)
                    {
                        data.Name = value;
                    }
                }

                model = new AttributeModel<FromPathAttributeData>
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