using System;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    /// <summary>
    /// Builder for <see cref="RestApiAttribute"/>.
    /// </summary>
    public class RestApiAttributeBuilder
    {
        public static RestApiAttribute Build(AttributeData att)
        {
            if (att.ConstructorArguments.Length != 2)
            {
                throw new NotSupportedException($"{TypeFullNames.RestApiAttribute} must have constructor with 2 arguments.");
            }

            var method = (LambdaHttpMethod)att.ConstructorArguments[0].Value;
            var template = att.ConstructorArguments[1].Value as string;

            var data = new RestApiAttribute(method, template);

            return data;
        }
    }
}