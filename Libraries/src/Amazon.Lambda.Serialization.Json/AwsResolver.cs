using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;

namespace Amazon.Lambda.Serialization.Json
{
    /// <summary>
    /// Custom contract resolver for handling special event cases.
    /// </summary>
    internal class AwsResolver : DefaultContractResolver
    {
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
            // Kinesis events need a custom converter to deserialize the data MemoryStream
            else if (type.FullName.Equals("Amazon.Lambda.KinesisEvents.KinesisEvent+Record", StringComparison.Ordinal))
            {
                foreach (JsonProperty property in properties)
                {
                    if (property.PropertyName.Equals("Data", StringComparison.Ordinal))
                    {
                        property.MemberConverter = new KinesisEventRecordDataConverter();
                    }
                }
            }

            return properties;
        }
    }
}