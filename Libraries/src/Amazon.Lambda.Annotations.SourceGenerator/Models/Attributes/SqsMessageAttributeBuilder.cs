using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    internal class SqsMessageAttributeBuilder
    {
        public static SqsMessageAttribute Build(AttributeData att)
        {
            var data = new SqsMessageAttribute();
            foreach (var attNamedArgument in att.NamedArguments)
            {
                switch (attNamedArgument.Key)
                {
                    case nameof(ISqsMessage.QueueName):
                        data.QueueName = attNamedArgument.Value.Value.ToString();
                        break;
                    case nameof(ISqsMessage.BatchSize):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value?.ToString()))
                        {
                            data.BatchSize = int.Parse(attNamedArgument.Value.Value.ToString());
                        }
                        break;
                    case nameof(ISqsMessage.QueueLogicalId):
                        data.QueueLogicalId = attNamedArgument.Value.Value?.ToString();
                        break;
                    case nameof(ISqsMessage.VisibilityTimeout):
                        data.VisibilityTimeout = int.Parse(attNamedArgument.Value.Value.ToString());
                        break;
                    case nameof(ISqsMessage.ContentBasedDeduplication):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value.ToString()))
                        {
                            data.ContentBasedDeduplication = bool.Parse(attNamedArgument.Value.Value.ToString());
                        }
                        break;
                    case nameof(ISqsMessage.DeduplicationScope):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value?.ToString()))
                        {
                            data.DeduplicationScope = attNamedArgument.Value.Value.ToString();
                        }
                        break;
                    case nameof(ISqsMessage.DelaySeconds):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value?.ToString()))
                        {
                            data.DelaySeconds = int.Parse(attNamedArgument.Value.Value.ToString());
                        }
                        break;
                    case nameof(ISqsMessage.FifoQueue):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value?.ToString()))
                        {
                            data.FifoQueue = bool.Parse(attNamedArgument.Value.Value.ToString());
                        }
                        break;
                    default:
                        throw new NotSupportedException(attNamedArgument.Key);
                }
            }

            return data;
        }
    }
}
