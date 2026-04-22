// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations.DynamoDB;
using Microsoft.CodeAnalysis;
using System;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    /// <summary>
    /// Builder for <see cref="DynamoDBEventAttribute"/>.
    /// </summary>
    public class DynamoDBEventAttributeBuilder
    {
        public static DynamoDBEventAttribute Build(AttributeData att)
        {
            if (att.ConstructorArguments.Length != 1)
            {
                throw new NotSupportedException($"{TypeFullNames.DynamoDBEventAttribute} must have constructor with 1 argument.");
            }
            var stream = att.ConstructorArguments[0].Value as string;
            var data = new DynamoDBEventAttribute(stream);

            foreach (var pair in att.NamedArguments)
            {
                if (pair.Key == nameof(data.ResourceName) && pair.Value.Value is string resourceName)
                {
                    data.ResourceName = resourceName;
                }
                else if (pair.Key == nameof(data.BatchSize) && pair.Value.Value is uint batchSize)
                {
                    data.BatchSize = batchSize;
                }
                else if (pair.Key == nameof(data.StartingPosition) && pair.Value.Value is int startingPosition)
                {
                    data.StartingPosition = (StartingPosition)startingPosition;
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
            }

            return data;
        }
    }
}
