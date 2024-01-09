using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Amazon.Lambda.Serialization.Json
{
    /// <summary>
    /// Custom contract resolver for handling special event cases.
    /// </summary>
    internal class AwsResolver : DefaultContractResolver
    {
        private JsonToMemoryStreamDataConverter jsonToMemoryStreamDataConverter;
        private JsonNumberToDateTimeDataConverter jsonNumberToDateTimeDataConverter;
        private JsonToMemoryStreamListDataConverter jsonToMemoryStreamListDataConverter;

        JsonToMemoryStreamDataConverter StreamDataConverter
        {
            get
            {
                if (jsonToMemoryStreamDataConverter == null)
                {
                    jsonToMemoryStreamDataConverter = new JsonToMemoryStreamDataConverter();
                }

                return jsonToMemoryStreamDataConverter;
            }
        }
        
        JsonToMemoryStreamListDataConverter StreamListDataConverter
        {
            get
            {
                if (jsonToMemoryStreamListDataConverter == null)
                {
                    jsonToMemoryStreamListDataConverter = new JsonToMemoryStreamListDataConverter();
                }

                return jsonToMemoryStreamListDataConverter;
            }
        }
        
        JsonNumberToDateTimeDataConverter DateTimeConverter
        {
            get
            {
                if (jsonNumberToDateTimeDataConverter == null)
                {
                    jsonNumberToDateTimeDataConverter = new JsonNumberToDateTimeDataConverter();
                }

                return jsonNumberToDateTimeDataConverter;
            }
        }
        
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            IList<JsonProperty> properties = base.CreateProperties(type, memberSerialization);
            // S3 events use non-standard key formatting for request IDs and need to be mapped to the correct properties
            if (type.FullName.Equals("Amazon.S3.Util.S3EventNotification+ResponseElementsEntity", StringComparison.Ordinal))
            {
                foreach (JsonProperty property in properties)
                {
                    if (property.PropertyName.Equals("XAmzRequestId", StringComparison.Ordinal))
                    {
                        property.PropertyName = "x-amz-request-id";
                    }
                    else if (property.PropertyName.Equals("XAmzId2", StringComparison.Ordinal))
                    {
                        property.PropertyName = "x-amz-id-2";
                    }
                }
            }
            else if (type.FullName.Equals("Amazon.Lambda.KinesisEvents.KinesisEvent+Record", StringComparison.Ordinal))
            {
                foreach (JsonProperty property in properties)
                {
                    if (property.PropertyName.Equals("Data", StringComparison.Ordinal))
                    {
                        property.MemberConverter = StreamDataConverter;
                    }
                    else if (property.PropertyName.Equals("ApproximateArrivalTimestamp", StringComparison.Ordinal))
                    {
                        property.MemberConverter = DateTimeConverter;
                    }
                }
            }
            else if (type.FullName.Equals("Amazon.Lambda.DynamoDBEvents.DynamoDBEvent+StreamRecord", StringComparison.Ordinal))
            {
                foreach (JsonProperty property in properties)
                {
                    if (property.PropertyName.Equals("ApproximateCreationDateTime", StringComparison.Ordinal))
                    {
                        property.MemberConverter = DateTimeConverter;
                    }
                }
            }
            else if (type.FullName.Equals("Amazon.Lambda.DynamoDBEvents.DynamoDBEvent+AttributeValue", StringComparison.Ordinal))
            {
                foreach (JsonProperty property in properties)
                {
                    if (property.PropertyName.Equals("B", StringComparison.Ordinal))
                    {
                        property.MemberConverter = StreamDataConverter;
                    }
                    else if (property.PropertyName.Equals("BS", StringComparison.Ordinal))
                    {
                        property.MemberConverter = StreamListDataConverter;
                    }
                }
            }
            else if (type.FullName.Equals("Amazon.Lambda.SQSEvents.SQSEvent+MessageAttribute", StringComparison.Ordinal))
            {
                foreach (JsonProperty property in properties)
                {
                    if (property.PropertyName.Equals("BinaryValue", StringComparison.Ordinal))
                    {
                        property.MemberConverter = StreamDataConverter;
                    }
                    else if (property.PropertyName.Equals("BinaryListValues", StringComparison.Ordinal))
                    {
                        property.MemberConverter = StreamListDataConverter;
                    }
                }
            }
            else if (type.FullName.StartsWith("Amazon.Lambda.CloudWatchEvents.")
                     && (type.GetTypeInfo().BaseType?.FullName?.StartsWith("Amazon.Lambda.CloudWatchEvents.CloudWatchEvent`",
                             StringComparison.Ordinal) ?? false))
            {
                foreach (JsonProperty property in properties)
                {
                    if (property.PropertyName.Equals("DetailType", StringComparison.Ordinal))
                    {
                        property.PropertyName = "detail-type";
                    }
                }
            }
            else if (type.FullName.Equals("Amazon.Lambda.KafkaEvents.KafkaEvent+KafkaEventRecord", StringComparison.Ordinal))
            {
                foreach (JsonProperty property in properties)
                {
                    if (property.PropertyName.Equals("Value", StringComparison.Ordinal))
                    {
                        property.MemberConverter = StreamDataConverter;
                    }
                }
            }

            return properties;
        }
    }
}