using System;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    public static class HttpApiAttributeBuilder
    {
        public static HttpApiAttribute Build(AttributeData att)
        {
            var data = new HttpApiAttribute(HttpApiVersion.V2);
            foreach (var pair in att.NamedArguments)
            {
                if (pair.Key == nameof(data.Version))
                {
                    if (pair.Value.Value is HttpApiVersion version)
                    {
                        data.Version = version;
                    }
                }
            }

            return data;
        }
    }
}