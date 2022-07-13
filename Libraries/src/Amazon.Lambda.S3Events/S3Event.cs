namespace Amazon.Lambda.S3Events
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// AWS S3 event
    /// http://docs.aws.amazon.com/lambda/latest/dg/with-s3.html
    /// http://docs.aws.amazon.com/lambda/latest/dg/eventsources.html#eventsources-s3-put
    /// </summary>

    public class S3Event
    {

        /// <summary>
        /// Gets and sets the records for the S3 event notification
        /// </summary>
        public List<S3EventNotificationRecord> Records { get; set; }

        /// <summary>
        /// The class holds the user identity properties.
        /// </summary>
        public class UserIdentityEntity
        {
            /// <summary>
            /// Gets and sets the PrincipalId property.
            /// </summary>
            public string PrincipalId { get; set; }
        }

        /// <summary>
        /// This class contains the identity information for an S3 bucket.
        /// </summary>
        public class S3BucketEntity
        {
            /// <summary>
            /// Gets and sets the name of the bucket.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets and sets the bucket owner id.
            /// </summary>
            public UserIdentityEntity OwnerIdentity { get; set; }

            /// <summary>
            /// Gets and sets the S3 bucket arn.
            /// </summary>
            public string Arn { get; set; }
        }

        /// <summary>
        /// This class contains the information for an object in S3.
        /// </summary>
        public class S3ObjectEntity
        {
            /// <summary>
            /// Gets and sets the key for the object stored in S3.
            /// </summary>
            public string Key { get; set; }

            /// <summary>
            /// Gets and sets the size of the object in S3.
            /// </summary>
            public long Size { get; set; }

            /// <summary>
            /// Gets and sets the etag of the object. This can be used to determine if the object has changed.
            /// </summary>
            public string ETag { get; set; }

            /// <summary>
            /// Gets and sets the version id of the object in S3.
            /// </summary>
            public string VersionId { get; set; }

            /// <summary>
            /// Gets and sets the sequencer a string representation of a hexadecimal value used to determine event sequence, only used with PUTs and DELETEs.
            /// </summary>
            public string Sequencer { get; set; }
        }

        /// <summary>
        /// Gets and sets the meta information describing S3.
        /// </summary>
        public class S3Entity
        {
            /// <summary>
            /// Gets and sets the ConfigurationId. This ID can be found in the bucket notification configuration.
            /// </summary>
            public string ConfigurationId { get; set; }

            /// <summary>
            /// Gets and sets the Bucket property.
            /// </summary>
            public S3BucketEntity Bucket { get; set; }

            /// <summary>
            /// Gets and sets the Object property.
            /// </summary>
            public S3ObjectEntity Object { get; set; }

            /// <summary>
            /// Gets and sets the S3SchemaVersion property.
            /// </summary>
            public string S3SchemaVersion { get; set; }
        }

        /// <summary>
        /// The class holds the request parameters
        /// </summary>
        public class RequestParametersEntity
        {
            /// <summary>
            /// Gets and sets the SourceIPAddress. This is the ip address where the request came from.
            /// </summary>
            public string SourceIPAddress { get; set; }
        }

        /// <summary>
        /// This class holds the response elements.
        /// </summary>

        [DataContract]
        public class ResponseElementsEntity
        {
            /// <summary>
            /// Gets and sets the XAmzId2 Property. This is the Amazon S3 host that processed the request.
            /// </summary>
            [DataMember(Name = "x-amz-id-2", EmitDefaultValue = false)]
            [System.Text.Json.Serialization.JsonPropertyName("x-amz-id-2")]
            public string XAmzId2 { get; set; }

            /// <summary>
            /// Gets and sets the XAmzRequestId. This is the Amazon S3 generated request ID.
            /// </summary>
            [DataMember(Name = "x-amz-request-id", EmitDefaultValue = false)]
            [System.Text.Json.Serialization.JsonPropertyName("x-amz-request-id")]
            public string XAmzRequestId { get; set; }
        }

        /// <summary>
        /// The class holds the glacier event data elements.
        /// </summary>
        public class S3GlacierEventDataEntity
        {
            /// <summary>
            /// Gets and sets the RestoreEventData property.
            /// </summary>
            public S3RestoreEventDataEntity RestoreEventData { get; set; }
        }

        /// <summary>
        /// The class holds the restore event data elements.
        /// </summary>
        public class S3RestoreEventDataEntity
        {
            /// <summary>
            /// Gets and sets the LifecycleRestorationExpiryTime the time when the object restoration will be expired.
            /// </summary>
            public DateTime LifecycleRestorationExpiryTime { get; set; }

            /// <summary>
            /// Gets and sets the LifecycleRestoreStorageClass the source storage class for restore.
            /// </summary>
            public string LifecycleRestoreStorageClass { get; set; }
        }

        /// <summary>
        /// The class holds the event notification.
        /// </summary>
        public class S3EventNotificationRecord
        {
            /// <summary>
            /// Gets and sets the AwsRegion property.
            /// </summary>
            public string AwsRegion { get; set; }

            /// <summary>
            /// Gets and sets the EventName property. This identities what type of event occurred.
            /// For example for an object just put in S3 this will be set to EventType.ObjectCreatedPut.
            /// </summary>
            public string EventName { get; set; }

            /// <summary>
            /// Gets and sets the EventSource property.
            /// </summary>
            public string EventSource { get; set; }

            /// <summary>
            /// Gets and sets the EventTime property. The time when S3 finished processing the request.
            /// </summary>
            public DateTime EventTime { get; set; }

            /// <summary>
            /// Gets and sets the EventVersion property.
            /// </summary>
            public string EventVersion { get; set; }

            /// <summary>
            /// Gets and sets the RequestParameters property.
            /// </summary>
            public RequestParametersEntity RequestParameters { get; set; }

            /// <summary>
            /// Gets and sets the ResponseElements property.
            /// </summary>
            public ResponseElementsEntity ResponseElements { get; set; }

            /// <summary>
            /// Gets and sets the S3 property.
            /// </summary>
            public S3Entity S3 { get; set; }

            /// <summary>
            /// Gets and sets the UserIdentity property.
            /// </summary>
            public UserIdentityEntity UserIdentity { get; set; }

            /// <summary>
            /// Get and sets the GlacierEventData property.
            /// </summary>
            public S3GlacierEventDataEntity GlacierEventData { get; set; }
        }
    }
}