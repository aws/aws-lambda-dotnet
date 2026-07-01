// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Amazon.Lambda.Annotations;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    /// <summary>
    /// Builder for <see cref="DurableExecutionAttribute"/>. Reads the required <c>executionTimeout</c>
    /// constructor argument plus any named arguments from the <see cref="AttributeData"/>; assigning
    /// <see cref="DurableExecutionAttribute.RetentionPeriodInDays"/> also sets its <c>IsXxxSet</c> flag so
    /// an unset value is omitted from the generated template.
    /// </summary>
    public class DurableExecutionAttributeBuilder
    {
        public static DurableExecutionAttribute Build(AttributeData att)
        {
            if (att.ConstructorArguments.Length != 1)
            {
                throw new NotSupportedException($"{TypeFullNames.DurableExecutionAttribute} must have a constructor with 1 argument.");
            }

            var executionTimeout = att.ConstructorArguments[0].Value is int timeout ? timeout : 0;
            var data = new DurableExecutionAttribute(executionTimeout);

            foreach (var pair in att.NamedArguments)
            {
                if (pair.Key == nameof(data.RetentionPeriodInDays) && pair.Value.Value is int retentionPeriodInDays)
                {
                    data.RetentionPeriodInDays = retentionPeriodInDays;
                }
            }

            return data;
        }
    }
}
