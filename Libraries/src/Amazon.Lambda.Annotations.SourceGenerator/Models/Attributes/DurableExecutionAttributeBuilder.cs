// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    /// <summary>
    /// Builder for <see cref="DurableExecutionAttribute"/>. Reads named arguments from the
    /// <see cref="AttributeData"/>; assigning each property also sets its corresponding
    /// <c>IsXxxSet</c> flag so unset values can be omitted from the generated template.
    /// </summary>
    public class DurableExecutionAttributeBuilder
    {
        public static DurableExecutionAttribute Build(AttributeData att)
        {
            var data = new DurableExecutionAttribute();

            foreach (var pair in att.NamedArguments)
            {
                if (pair.Key == nameof(data.RetentionPeriodInDays) && pair.Value.Value is int retentionPeriodInDays)
                {
                    data.RetentionPeriodInDays = retentionPeriodInDays;
                }
                else if (pair.Key == nameof(data.ExecutionTimeout) && pair.Value.Value is int executionTimeout)
                {
                    data.ExecutionTimeout = executionTimeout;
                }
            }

            return data;
        }
    }
}
