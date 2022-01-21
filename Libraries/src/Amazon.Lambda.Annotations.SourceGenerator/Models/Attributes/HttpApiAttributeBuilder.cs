using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    public static class HttpApiAttributeBuilder
    {
        public static HttpApiAttribute Build(AttributeData att)
        {
            if (att.ConstructorArguments.Length != 2)
            {
                throw new NotSupportedException($"{TypeFullNames.HttpApiAttribute} must have constructor with 2 arguments.");
            }

            var method = (LambdaHttpMethod)att.ConstructorArguments[0].Value;
            var template = att.ConstructorArguments[1].Value as string;
            var version = att.NamedArguments.FirstOrDefault(arg => arg.Key == "Version").Value.Value;

            var data = new HttpApiAttribute(method, template)
            {
                Version = version == null ? HttpApiVersion.V2 : (HttpApiVersion)version
            };
            return data;
        }
    }
}