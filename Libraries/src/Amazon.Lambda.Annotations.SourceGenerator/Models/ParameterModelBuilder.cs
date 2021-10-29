using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// <see cref="ParameterModel"/> builder.
    /// </summary>
    public static class ParameterModelBuilder
    {
        public static IList<ParameterModel> Build(IMethodSymbol methodSymbol,
            GeneratorExecutionContext context)
        {
            var models = new List<ParameterModel>();

            foreach (var parameter in methodSymbol.Parameters)
            {
                models.Add(new ParameterModel
                {
                    Name = parameter.Name,
                    Type = TypeModelBuilder.Build(parameter.Type, context),
                    Attributes = parameter.GetAttributes().Select(att => AttributeModelBuilder.Build(att, context)).ToList()
                });
            }

            return models;
        }
    }
}