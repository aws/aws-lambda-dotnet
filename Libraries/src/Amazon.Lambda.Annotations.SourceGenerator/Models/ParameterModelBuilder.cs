using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// <see cref="ParameterModel"/> builder.
    /// </summary>
    public static class ParameterModelBuilder
    {
        public static IList<ParameterModel> Build(IMethodSymbol methodSymbol)
        {
            var models = new List<ParameterModel>();

            foreach (var parameter in methodSymbol.Parameters)
            {
                models.Add(new ParameterModel
                {
                    Name = parameter.Name,
                    Type = TypeModelBuilder.Build(parameter),
                    Attributes = parameter.GetAttributes().Select(att => TypeModelBuilder.Build(att.AttributeClass)).ToList()
                });
            }

            return models;
        }
    }
}