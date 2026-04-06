using Amazon.Lambda.Annotations.ALB;
using Microsoft.CodeAnalysis;
using System;
using System.Linq;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    /// <summary>
    /// Builder for <see cref="ALBApiAttribute"/>.
    /// </summary>
    public class ALBApiAttributeBuilder
    {
        public static ALBApiAttribute Build(AttributeData att)
        {
            if (att.ConstructorArguments.Length != 3)
            {
                throw new NotSupportedException($"{TypeFullNames.ALBApiAttribute} must have constructor with 3 arguments.");
            }

            var listenerArn = att.ConstructorArguments[0].Value as string;
            var pathPattern = att.ConstructorArguments[1].Value as string;
            var priority = (int)att.ConstructorArguments[2].Value;

            var data = new ALBApiAttribute(listenerArn, pathPattern, priority);

            foreach (var pair in att.NamedArguments)
            {
                if (pair.Key == nameof(data.MultiValueHeaders) && pair.Value.Value is bool multiValueHeaders)
                {
                    data.MultiValueHeaders = multiValueHeaders;
                }
                else if (pair.Key == nameof(data.HostHeader) && pair.Value.Value is string hostHeader)
                {
                    data.HostHeader = hostHeader;
                }
                else if (pair.Key == nameof(data.HttpMethod) && pair.Value.Value is string httpMethod)
                {
                    data.HttpMethod = httpMethod;
                }
                else if (pair.Key == nameof(data.ResourceName) && pair.Value.Value is string resourceName)
                {
                    data.ResourceName = resourceName;
                }
                else if (pair.Key == nameof(data.HttpHeaderConditionName) && pair.Value.Value is string httpHeaderConditionName)
                {
                    data.HttpHeaderConditionName = httpHeaderConditionName;
                }
                else if (pair.Key == nameof(data.HttpHeaderConditionValues) && !pair.Value.IsNull)
                {
                    data.HttpHeaderConditionValues = pair.Value.Values.Select(v => v.Value as string).ToArray();
                }
                else if (pair.Key == nameof(data.QueryStringConditions) && !pair.Value.IsNull)
                {
                    data.QueryStringConditions = pair.Value.Values.Select(v => v.Value as string).ToArray();
                }
                else if (pair.Key == nameof(data.SourceIpConditions) && !pair.Value.IsNull)
                {
                    data.SourceIpConditions = pair.Value.Values.Select(v => v.Value as string).ToArray();
                }
            }

            return data;
        }
    }
}
