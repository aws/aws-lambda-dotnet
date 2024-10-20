using Amazon.Lambda.Annotations.SQS;
using Microsoft.CodeAnalysis;
using System;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    /// <summary>
    /// Builder for <see cref="SQSEventAttribute"/>.
    /// </summary>
    public class SQSEventAttributeBuilder
    {
        public static SQSEventAttribute Build(AttributeData att)
        {
            if (att.ConstructorArguments.Length != 1)
            {
                throw new NotSupportedException($"{TypeFullNames.SQSEventAttribute} must have constructor with 1 argument.");
            }
            var queue = att.ConstructorArguments[0].Value as string;
            var data = new SQSEventAttribute(queue);

            foreach (var pair in att.NamedArguments)
            {
                if (pair.Key == nameof(data.ResourceName) && pair.Value.Value is string resourceName)
                {
                    data.ResourceName = resourceName;
                }
                if (pair.Key == nameof(data.BatchSize) && pair.Value.Value is uint batchSize)
                {
                    data.BatchSize = batchSize;
                }
                else if (pair.Key == nameof(data.Enabled) && pair.Value.Value is bool enabled)
                {
                    data.Enabled = enabled;
                }
                else if (pair.Key == nameof(data.MaximumBatchingWindowInSeconds) && pair.Value.Value is uint maximumBatchingWindowInSeconds)
                {
                    data.MaximumBatchingWindowInSeconds = maximumBatchingWindowInSeconds;
                }
                else if (pair.Key == nameof(data.Filters) && pair.Value.Value is string filters)
                {
                    data.Filters = filters;
                }
                else if (pair.Key == nameof(data.MaximumConcurrency) && pair.Value.Value is uint maximumConcurrency)
                {
                    data.MaximumConcurrency = maximumConcurrency;
                }
            }

            return data;
        }
    }
}
