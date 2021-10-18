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
        public static List<EventType> Build(IMethodSymbol lambdaMethodSymbol,
            GeneratorExecutionContext context)
        {
            var events = new List<EventType>();
            if (lambdaMethodSymbol.HasAttribute(context, TypeFullNames.RestApiAttribute)
                || lambdaMethodSymbol.HasAttribute(context, TypeFullNames.HttpApiAttribute))
            {
                events.Add(EventType.API);
            }

            return events;
        }
    }
}