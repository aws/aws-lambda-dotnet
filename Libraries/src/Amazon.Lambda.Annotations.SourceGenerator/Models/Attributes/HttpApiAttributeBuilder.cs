using System;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    public static class HttpApiAttributeBuilder
    {
        public static HttpApiAttribute Build(AttributeData att)
        {
            if (att.ConstructorArguments.Length != 3)
            {
                throw new NotSupportedException($"{TypeFullNames.HttpApiAttribute} must have constructor with 3 arguments.");
            }

            var method = (HttpMethod)att.ConstructorArguments[0].Value;
            var version = (HttpApiVersion)att.ConstructorArguments[1].Value;
            var template = att.ConstructorArguments[2].Value as string;

            var data = new HttpApiAttribute(method, version, template);
            return data;
        }
    }
}