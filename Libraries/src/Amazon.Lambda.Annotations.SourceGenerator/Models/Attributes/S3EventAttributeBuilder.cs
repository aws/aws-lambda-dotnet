using Amazon.Lambda.Annotations.S3;
using Microsoft.CodeAnalysis;
using System;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    public class S3EventAttributeBuilder
    {
        public static S3EventAttribute Build(AttributeData att)
        {
            if (att.ConstructorArguments.Length != 1)
                throw new NotSupportedException($"{TypeFullNames.S3EventAttribute} must have constructor with 1 argument.");

            var bucket = att.ConstructorArguments[0].Value as string;
            var data = new S3EventAttribute(bucket);

            foreach (var pair in att.NamedArguments)
            {
                if (pair.Key == nameof(data.ResourceName) && pair.Value.Value is string resourceName)
                    data.ResourceName = resourceName;
                else if (pair.Key == nameof(data.Events) && pair.Value.Value is string events)
                    data.Events = events;
                else if (pair.Key == nameof(data.FilterPrefix) && pair.Value.Value is string filterPrefix)
                    data.FilterPrefix = filterPrefix;
                else if (pair.Key == nameof(data.FilterSuffix) && pair.Value.Value is string filterSuffix)
                    data.FilterSuffix = filterSuffix;
                else if (pair.Key == nameof(data.Enabled) && pair.Value.Value is bool enabled)
                    data.Enabled = enabled;
            }

            return data;
        }
    }
}
