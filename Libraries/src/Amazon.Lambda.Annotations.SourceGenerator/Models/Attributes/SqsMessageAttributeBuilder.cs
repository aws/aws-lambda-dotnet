using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
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
                    case nameof(ISqsMessage.EventQueueName):
                        data.EventQueueName = attNamedArgument.Value.Value.ToString();
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
                    case nameof(ISqsMessage.FifoThroughputLimit):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value?.ToString()))
                        {
                            data.FifoThroughputLimit = attNamedArgument.Value.Value.ToString();
                        }
                        break;
                    case nameof(ISqsMessage.KmsDataKeyReusePeriodSeconds):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value?.ToString()))
                        {
                            data.KmsDataKeyReusePeriodSeconds = int.Parse(attNamedArgument.Value.Value.ToString());
                        }
                        break;
                    case nameof(ISqsMessage.KmsMasterKeyId):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value?.ToString()))
                        {
                            data.KmsMasterKeyId = attNamedArgument.Value.Value.ToString();
                        }
                        break;
                    // MaximumMessageSize
                    case nameof(ISqsMessage.MaximumMessageSize):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value?.ToString()))
                        {
                            data.MaximumMessageSize = int.Parse(attNamedArgument.Value.Value.ToString());
                        }
                        break;
                    //MessageRetentionPeriod
                    case nameof(ISqsMessage.MessageRetentionPeriod):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value?.ToString()))
                        {
                            data.MessageRetentionPeriod = int.Parse(attNamedArgument.Value.Value.ToString());
                        }
                        break;
                    //ReceiveMessageWaitTimeSeconds
                    case nameof(ISqsMessage.ReceiveMessageWaitTimeSeconds):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value?.ToString()))
                        {
                            data.ReceiveMessageWaitTimeSeconds = int.Parse(attNamedArgument.Value.Value.ToString());
                        }
                        break;
                    //RedriveAllowPolicy
                    case nameof(ISqsMessage.RedriveAllowPolicy):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value?.ToString()))
                        {
                            data.RedriveAllowPolicy = attNamedArgument.Value.Value.ToString();
                        }
                        break;
                    // RedrivePolicy
                    case nameof(ISqsMessage.RedrivePolicy):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value?.ToString()))
                        {
                            data.RedrivePolicy = attNamedArgument.Value.Value.ToString();
                        }
                        break;
                    // Tags
                    case nameof(ISqsMessage.Tags):
                        if (attNamedArgument.Value.Values.Any())
                        {
                            var final = new List<string>();

                            foreach (var pair in attNamedArgument.Value.Values)
                            {
                                final.Add(pair.Value.ToString());
                            }

                            data.Tags = final.ToArray();
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
