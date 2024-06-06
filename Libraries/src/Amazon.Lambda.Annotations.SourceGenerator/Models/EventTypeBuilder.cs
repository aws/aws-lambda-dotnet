using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Annotations.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// <see cref="EventType"/> builder.
    /// </summary>
    public class EventTypeBuilder
    {
        public static HashSet<EventType> Build(IMethodSymbol lambdaMethodSymbol,
            GeneratorExecutionContext context)
        {
            var events = new HashSet<EventType>();
            foreach (var attribute in lambdaMethodSymbol.GetAttributes())
            {
                if (attribute.AttributeClass.ToDisplayString() == TypeFullNames.RestApiAttribute
                    || attribute.AttributeClass.ToDisplayString() == TypeFullNames.HttpApiAttribute)
                {
                    events.Add(EventType.API);
                }
                else if (attribute.AttributeClass.ToDisplayString() == TypeFullNames.SQSEventAttribute)
                {
                    events.Add(EventType.SQS);
                }
            }

            return events;
        }
    }
}