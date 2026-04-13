// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations.Schedule;
using Microsoft.CodeAnalysis;
using System;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    /// <summary>
    /// Builder for <see cref="ScheduleEventAttribute"/>.
    /// </summary>
    public class ScheduleEventAttributeBuilder
    {
        public static ScheduleEventAttribute Build(AttributeData att)
        {
            if (att.ConstructorArguments.Length != 1)
            {
                throw new NotSupportedException($"{TypeFullNames.ScheduleEventAttribute} must have constructor with 1 argument.");
            }
            var schedule = att.ConstructorArguments[0].Value as string;
            var data = new ScheduleEventAttribute(schedule);

            foreach (var pair in att.NamedArguments)
            {
                if (pair.Key == nameof(data.ResourceName) && pair.Value.Value is string resourceName)
                {
                    data.ResourceName = resourceName;
                }
                else if (pair.Key == nameof(data.Description) && pair.Value.Value is string description)
                {
                    data.Description = description;
                }
                else if (pair.Key == nameof(data.Input) && pair.Value.Value is string input)
                {
                    data.Input = input;
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
