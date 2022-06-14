using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json.Linq;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    internal class SqsMessageAttributeBuilder
    {
        private const string RedriveAllPolicyNotValidJsonExceptionMessage = "RedriveAllPolicy must be valid Json";

        public static SqsMessageAttribute Build(AttributeData att)
        {
            var data = new SqsMessageAttribute();
            foreach (var attNamedArgument in att.NamedArguments)
            {
                switch (attNamedArgument.Key)
                {
                    case nameof(ISqsMessage.EventQueueARN):
                        data.EventQueueARN = attNamedArgument.Value.Value.ToString();
                        break;
                    case nameof(ISqsMessage.EventBatchSize):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value?.ToString()))
                        {
                            data.EventBatchSize = uint.Parse(attNamedArgument.Value.Value.ToString());
                        }
                        break;
                    case nameof(ISqsMessage.QueueLogicalId):
                        data.QueueLogicalId = attNamedArgument.Value.Value?.ToString();
                        break;
                    case nameof(ISqsMessage.VisibilityTimeout):
                        data.VisibilityTimeout = uint.Parse(attNamedArgument.Value.Value.ToString());
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
                            data.DelaySeconds = uint.Parse(attNamedArgument.Value.Value.ToString());
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
                            data.KmsDataKeyReusePeriodSeconds = uint.Parse(attNamedArgument.Value.Value.ToString());
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
                            data.MaximumMessageSize = uint.Parse(attNamedArgument.Value.Value.ToString());
                        }
                        break;
                    // Queue
                    case nameof(ISqsMessage.QueueName):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value?.ToString()))
                        {
                            data.QueueName = attNamedArgument.Value.Value.ToString();
                        }
                        break;
                    // MessageRetentionPeriod
                    case nameof(ISqsMessage.MessageRetentionPeriod):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value?.ToString()))
                        {
                            data.MessageRetentionPeriod = uint.Parse(attNamedArgument.Value.Value.ToString());
                        }
                        break;
                    //ReceiveMessageWaitTimeSeconds
                    case nameof(ISqsMessage.ReceiveMessageWaitTimeSeconds):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value?.ToString()))
                        {
                            data.ReceiveMessageWaitTimeSeconds = uint.Parse(attNamedArgument.Value.Value.ToString());
                        }
                        break;
                    //RedriveAllowPolicy
                    case nameof(ISqsMessage.RedriveAllowPolicy):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value?.ToString()))
                        {
                            var json = attNamedArgument.Value.Value.ToString();
                            try
                            {
                                JObject.Parse(json);
                            }
                            catch (Exception e)
                            {

                                throw new ArgumentOutOfRangeException(nameof(ISqsMessage.RedriveAllowPolicy), SqsMessageAttributeBuilder.RedriveAllPolicyNotValidJsonExceptionMessage);
                            }
                            data.RedriveAllowPolicy = json;
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
                    case nameof(ISqsMessage.EventFilterCriteria):
                        if (attNamedArgument.Value.Values.Any())
                        {
                            var final = new List<string>();

                            foreach (var pair in attNamedArgument.Value.Values)
                            {
                                final.Add(pair.Value.ToString());
                            }

                            data.EventFilterCriteria = final.ToArray();
                        }
                        break;
                    /// MaximumBatchingWindowInSeconds
                    case nameof(ISqsMessage.EventMaximumBatchingWindowInSeconds):
                        if (!string.IsNullOrEmpty(attNamedArgument.Value.Value?.ToString()))
                        {
                            data.EventMaximumBatchingWindowInSeconds = uint.Parse(attNamedArgument.Value.Value.ToString());
                        }
                        break;


                    default:
                        throw new NotSupportedException(attNamedArgument.Key);
                }
            }

            if (data.FifoQueue && !string.IsNullOrEmpty(data.QueueName) && !data.QueueName.EndsWith(".fifo"))
            {
                throw new ArgumentOutOfRangeException(nameof(SqsMessageAttribute.QueueName), $"If using {nameof(SqsMessageAttribute.FifoQueue)} = true, {nameof(SqsMessageAttribute.QueueName)} must end in '.fifo'");
            }

            return data;
        }
    }
}
