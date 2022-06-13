namespace Amazon.Lambda.Annotations
{
    public interface ISqsMessage
    {
        /// <summary>
        /// If queue auto-creation (in .template) feature, specify the Logical Id of the queue in the template.
        /// </summary>
        string QueueLogicalId { get; set; }

        /// <summary>
        /// For Events:  The maximum number of items to retrieve in a single batch.
        /// Type: Integer
        /// Required: No
        /// Default: 10
        /// AWS CloudFormation compatibility: This property is passed directly to the BatchSize property of an AWS::Lambda::EventSourceMapping resource.
        /// Minimum: 1
        /// Maximum: 10000
        /// </summary>
        uint EventBatchSize { get; set; }

        /// <summary>
        /// A object that defines the criteria to determine whether Lambda should process an event. For more information, see AWS Lambda event filtering in the AWS Lambda Developer Guide.
        /// Type: FilterCriteria
        /// Required: No
        /// AWS CloudFormation compatibility: This property is passed directly to the FilterCriteria property of an AWS::Lambda::EventSourceMapping resource.
        /// </summary>
        string[] EventFilterCriteria { get; set; }
        /// <summary>
        /// For Events: The ARN of the queue.
        /// Type: String
        /// Required: Yes (If not using the auto-create feature via QueueLogicalId
        /// AWS CloudFormation compatibility: This property is passed directly to the EventSourceArn property of an AWS::Lambda::EventSourceMapping resource.
        /// <seealso cref="QueueLogicalId"/>
        /// </summary>
        string EventQueueARN { get; set; }

        /// <summary>
        /// For first-in-first-out (FIFO) queues, specifies whether to enable content-based deduplication. During the deduplication interval, Amazon SQS treats messages that are sent with identical content as duplicates and delivers only one copy of the message. For more information, see the ContentBasedDeduplication attribute for the CreateQueue action in the Amazon SQS API Reference.
        /// Required: No
        /// Type: Boolean
        /// Update requires: No interruption
        /// </summary>


        bool ContentBasedDeduplication { get; set; }

        /// <summary>
        /// For high throughput for FIFO queues, specifies whether message deduplication occurs at the message group or queue level. Valid values are messageGroup and queue.
        /// To enable high throughput for a FIFO queue, set this attribute to messageGroup and set the FifoThroughputLimit attribute to perMessageGroupId. If you set these attributes to anything other than these values, normal throughput is in effect and deduplication occurs as specified.For more information, see High throughput for FIFO queues and Quotas related to messages in the Amazon SQS Developer Guide.
        /// 
        /// Required: No
        /// 
        /// Type: String
        /// Update requires: No interruption
        /// </summary>
        string DeduplicationScope { get; set; }

        /// <summary>
        /// If set to true, creates a FIFO queue. If you don't specify this property, Amazon SQS creates a standard queue. For more information, see FIFO queues in the Amazon SQS Developer Guide.
        /// Required: No
        /// Type: Boolean
        /// Update requires: Replacement
        /// </summary>
        uint DelaySeconds { get; set; }

        /// <summary>
        /// If set to true, creates a FIFO queue. If you don't specify this property, Amazon SQS creates a standard queue. For more information, see FIFO queues in the Amazon SQS Developer Guide.
        /// Required: No
        /// Type: Boolean
        /// Update requires: Replacement
        /// </summary>
        bool FifoQueue { get; set; }

        /// <summary>
        /// For high throughput for FIFO queues, specifies whether the FIFO queue throughput quota applies to the entire queue or per message group. Valid values are perQueue and perMessageGroupId.
        /// To enable high throughput for a FIFO queue, set this attribute to perMessageGroupId and set the DeduplicationScope attribute to messageGroup. If you set these attributes to anything other than these values, normal throughput is in effect and deduplication occurs as specified. For more information, see High throughput for FIFO queues and Quotas related to messages in the Amazon SQS Developer Guide.
        /// Required: No
        /// Type: String
        /// Update requires: No interruption
        /// </summary>
        string FifoThroughputLimit { get; set; }


        /// <summary>
        /// The length of time in seconds for which Amazon SQS can reuse a data key to encrypt or decrypt messages before calling AWS KMS again. The value must be an integer between 60 (1 minute) and 86,400 (24 hours). The default is 300 (5 minutes).
        /// Note: A shorter time period provides better security, but results in more calls to AWS KMS, which might incur charges after Free Tier. For more information, see Encryption at rest in the Amazon SQS Developer Guide.
        /// Required: No
        /// Type: Integer
        /// Update requires: No interruption
        /// </summary>
        uint KmsDataKeyReusePeriodSeconds { get; set; }

        /// <summary>
        /// The ID of an AWS managed customer master key (CMK) for Amazon SQS or a custom CMK. To use the AWS managed CMK for Amazon SQS, specify the (default) alias alias/aws/sqs. For more information, see the following:
        /// 1. Encryption at rest in the Amazon SQS Developer Guide
        /// 2. CreateQueue in the Amazon SQS API Reference
        /// 3. The Customer Master Keys section of the AWS Key Management Service Best Practices whitepaper
        /// Required: No
        /// Type: String
        /// Update requires: No interruption
        /// </summary>
        string KmsMasterKeyId { get; set; }

        /// <summary>
        /// The limit of how many bytes that a message can contain before Amazon SQS rejects it. You can specify an integer value from 1,024 bytes (1 KiB) to 262,144 bytes (256 KiB). The default value is 262,144 (256 KiB).
        /// Required: No
        /// Type: Integer
        /// Update requires: No interruption
        /// </summary>
        uint MaximumMessageSize { get; set; }

        /// <summary>
        /// The number of seconds that Amazon SQS retains a message. You can specify an integer value from 60 seconds (1 minute) to 1,209,600 seconds (14 days). The default value is 345,600 seconds (4 days).
        /// Required: No
        /// Type: Integer
        /// Update requires: No interruption
        /// </summary>
        uint MessageRetentionPeriod { get; set; }

        /// <summary>
        /// A name for the queue. To create a FIFO queue, the name of your FIFO queue must end with the .fifo suffix. For more information, see FIFO queues in the Amazon SQS Developer Guide.
        /// If you don't specify a name, AWS CloudFormation generates a unique physical ID and uses that ID for the queue name. For more information, see Name type in the AWS CloudFormation User Guide.
        /// Important: If you specify a name, you can't perform updates that require replacement of this resource. You can perform updates that require no or some interruption. If you must replace the resource, specify a new name.
        /// Required: No
        /// Type: String
        /// Update requires: Replacement
        /// </summary>
        string QueueName { get; set; }


        /// <summary>
        /// Specifies the duration, in seconds, that the ReceiveMessage action call waits until a message is in the queue in order to include it in the response, rather than returning an empty response if a message isn't yet available. You can specify an integer from 1 to 20. Short polling is used as the default or when you specify 0 for this property. For more information, see Consuming messages using long polling in the Amazon SQS Developer Guide.
        /// Required: No
        /// Type: Integer
        /// Update requires: No interruption

        /// </summary>
        uint ReceiveMessageWaitTimeSeconds { get; set; }

        /// <summary>
        /// The string that includes the parameters for the permissions for the dead-letter queue redrive permission and which source queues can specify dead-letter queues as a JSON object. The parameters are as follows:
        /// redrivePermission: The permission type that defines which source queues can specify the current queue as the dead-letter queue. Valid values are:
        /// allowAll: (Default) Any source queues in this AWS account in the same Region can specify this queue as the dead-letter queue.
        /// denyAll: No source queues can specify this queue as the dead-letter queue.
        /// byQueue: Only queues specified by the sourceQueueArns parameter can specify this queue as the dead-letter queue.
        /// sourceQueueArns: The Amazon Resource Names (ARN)s of the source queues that can specify this queue as the dead-letter queue and redrive messages. You can specify this parameter only when the redrivePermission parameter is set to byQueue. You can specify up to 10 source queue ARNs. To allow more than 10 source queues to specify dead-letter queues, set the redrivePermission parameter to allowAll.
        /// Required: No
        /// Type: Json
        /// Update requires: No interruption
        /// </summary>
        string RedriveAllowPolicy { get; set; }

        /// <summary>
        /// The string that includes the parameters for the dead-letter queue functionality of the source queue as a JSON object. The parameters are as follows:
        /// deadLetterTargetArn: The Amazon Resource Name (ARN) of the dead-letter queue to which Amazon SQS moves messages after the value of maxReceiveCount is exceeded.
        /// maxReceiveCount: The number of times a message is delivered to the source queue before being moved to the dead-letter queue. When the ReceiveCount for a message exceeds the maxReceiveCount for a queue, Amazon SQS moves the message to the dead-letter-queue.
        /// Note: The dead-letter queue of a FIFO queue must also be a FIFO queue. Similarly, the dead-letter queue of a standard queue must also be a standard queue.
        /// JSON: { "deadLetterTargetArn" : String, "maxReceiveCount" : Integer }
        /// YAML: NOT SUPPORTED
        /// Required: No
        /// Type: Json
        /// Update requires: No interruption
        /// </summary>
        string RedrivePolicy { get; set; }

        /// <summary>
        /// Key value pairs of tags
        /// The tags that you attach to this queue. For more information, see Resource tag in the AWS CloudFormation User Guide.
        /// Required: No
        /// Type: List of Tag
        /// Update requires: No interruption
        /// <example>
        /// Tags = new string[] {"Tag1=Value1", "Tag2=Value"}
        /// </example>
        /// </summary>
        string[] Tags { get; set; }

        /// <summary>
        /// The length of time during which a message will be unavailable after a message is delivered from the queue. This blocks other components from receiving the same message and gives the initial component time to process and delete the message from the queue.
        /// Values must be from 0 to 43,200 seconds (12 hours). If you don't specify a value, AWS CloudFormation uses the default value of 30 seconds.
        /// For more information about Amazon SQS queue visibility timeouts, see Visibility timeout in the Amazon SQS Developer Guide.
        /// Required: No
        /// Type: Integer
        /// Update requires: No interruption
        /// </summary>
        uint VisibilityTimeout { get; set; }


    }
}