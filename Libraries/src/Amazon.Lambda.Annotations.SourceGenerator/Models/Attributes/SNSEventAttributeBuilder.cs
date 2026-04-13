using Amazon.Lambda.Annotations.SNS;
using Microsoft.CodeAnalysis;
using System;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    /// <summary>
    /// Builder for <see cref="SNSEventAttribute"/>.
    /// </summary>
    public class SNSEventAttributeBuilder
    {
        public static SNSEventAttribute Build(AttributeData att)
        {
            if (att.ConstructorArguments.Length != 1)
            {
                throw new NotSupportedException($"{TypeFullNames.SNSEventAttribute} must have constructor with 1 argument.");
            }
            var topic = att.ConstructorArguments[0].Value as string;
            var data = new SNSEventAttribute(topic);

            foreach (var pair in att.NamedArguments)
            {
                if (pair.Key == nameof(data.ResourceName) && pair.Value.Value is string resourceName)
                {
                    data.ResourceName = resourceName;
                }
                else if (pair.Key == nameof(data.FilterPolicy) && pair.Value.Value is string filterPolicy)
                {
                    data.FilterPolicy = filterPolicy;
                }
                else if (pair.Key == nameof(data.Enabled) && pair.Value.Value is bool enabled)
                {
                    data.Enabled = enabled;
                }
            }

            return data;
        }
    }
}
